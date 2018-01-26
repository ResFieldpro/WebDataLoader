using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Principal;
using System.Text;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Services.Description;
using ICSharpCode.SharpZipLib.Zip;
using ICSharpCode.SharpZipLib.Core;

namespace WebDataLoader
{
	public class WebImporter
	{
		//static members
		static int welltype = 2;
		static int NullLongData = -2147483648;
		static int NameSpaceCounter = 0;
		static string ObjectManagerServiceName = "ObjectManagerService";
        static string MigImportServiceName = "MigImportService";
        static Dictionary<string, object> WebServices = new Dictionary<string, object>();
		static Dictionary<string, string> WebSessions = new Dictionary<string, string>();
		//helpers
		public bool TestWebServer(string WebServer)
		{
			var url = WebServer + ObjectManagerServiceName + ".asmx";
			try
			{
				var myRequest = (HttpWebRequest)WebRequest.Create(url);
				var response = (HttpWebResponse)myRequest.GetResponse();
				return true;
			}
			catch (Exception ex)
			{
				return false;
			}
		}
		string Webhash(string WebServer, string WebServerU, string WebServerP)
		{
			return $"server:{WebServer}user:{WebServerU}password:{WebServerP}";
		}
		void CorrectWebAddress(ref string WebServer)
		{
			if (WebServer.Substring(WebServer.Length - 1) != "/")
				WebServer += "/";
		}
		void GetCorrectedUserCreds(ref string WebServerU, ref string WebServerP)
		{
			if (string.IsNullOrEmpty(WebServerU)) // if user name is not specified - use windows authentication to try with efieldpro
			{
				WebServerP = "";
				SecurityIdentifier s = WindowsIdentity.GetCurrent().User;
				byte[] binaryForm = new byte[s.BinaryLength];
				s.GetBinaryForm(binaryForm, 0);
				string res = "";
				foreach (byte b in binaryForm)
				{
					res += b.ToString("x2");
				}
				WebServerU = "WINLGN:" + res;
			}
		}
		//data loader
		object GetObjectManagerService(string WebServer, string WebServerU, string WebServerP)
		{
			if (string.IsNullOrEmpty(WebServer))
				throw new Exception("Invalid web server address!");
			//store all instances of service class as each instance is a separate dll and loaded each time into memory
			string hash = Webhash(WebServer, WebServerU, WebServerP);
			if (WebServices.ContainsKey(hash))
			{
				return WebServices[hash];
			}

			ServiceDescriptionImporter importer = new ServiceDescriptionImporter();
			using (System.Net.WebClient client = new System.Net.WebClient())
			{
				CorrectWebAddress(ref WebServer);

				System.IO.Stream stream = client.OpenRead(WebServer + ObjectManagerServiceName + ".asmx?wsdl");
				importer.ProtocolName = "Soap12";
				ServiceDescription description = ServiceDescription.Read(stream);
				importer.AddServiceDescription(description, null, null);
				importer.Style = ServiceDescriptionImportStyle.Client;
			}

			importer.CodeGenerationOptions = System.Xml.Serialization.CodeGenerationOptions.GenerateProperties;
			CodeNamespace nmspace = new CodeNamespace();
			NameSpaceCounter++;
			CodeCompileUnit CompileUnit = new CodeCompileUnit();
			CompileUnit.Namespaces.Add(nmspace);
			ServiceDescriptionImportWarnings warning = importer.Import(nmspace, CompileUnit);
			if (warning == 0)
			{
				string[] assemblyReferences = new string[2] { "System.Web.Services.dll", "System.Xml.dll" };
				CompilerParameters parms = new CompilerParameters(assemblyReferences);
				CompilerResults results;
				using (CodeDomProvider DomProvider = CodeDomProvider.CreateProvider("CSharp"))
				{
					results = DomProvider.CompileAssemblyFromDom(parms, CompileUnit);
				}

				object wsvcClass = results.CompiledAssembly.CreateInstance(ObjectManagerServiceName);
				WebServices[hash] = wsvcClass;
				return wsvcClass;
			}
			throw new Exception(message: "Failed to connect to web servie. Check web server exists.");
		}
		public string GetSession(string WebServer, string WebServerU, string WebServerP)
		{
			string hash = Webhash(WebServer, WebServerU, WebServerP);
			if (WebSessions.ContainsKey(hash))
			{
				return WebSessions[hash];
			}
			object ObjectManagerService = GetObjectManagerService(WebServer, WebServerU, WebServerP);
			MethodInfo mi = ObjectManagerService.GetType().GetMethod("Login");
			GetCorrectedUserCreds(ref WebServerU, ref WebServerP);
			object[] argss = new object[2] { WebServerU, WebServerP };
			string websession = mi.Invoke(ObjectManagerService, argss).ToString();
			WebSessions[hash] = websession;
			return websession;
		}
		private DataTable BuildDataSet(string str)
		{
			DataTable dt = new DataTable();
			if (string.IsNullOrEmpty(str))
				return dt;

			str = Regex.Replace(str, @"^\s+$[\r\n]*", "", RegexOptions.Multiline);

			int counter = 0;
			bool[] istext = new bool[100];
			using (StringReader reader = new StringReader(str))
			{
				string sline;
				while ((sline = reader.ReadLine()) != null)
				{
					if (string.IsNullOrEmpty(sline))
						continue;

					string[] arr = sline.Split('\t');
					if (counter == 0)
					{
						foreach (string col in arr)
						{
							DataColumn dc = new DataColumn(col.Replace(".", ""), Type.GetType("System.String"));
							dt.Columns.Add(dc);
						}
					}
					else
					{
						DataRow dr = dt.NewRow();
						for (int i = 0; i < arr.Length; i++)
						{
							dr[i] = arr[i];
							double res;
							if (istext[i] != true && !(string.IsNullOrEmpty(arr[i])) && !double.TryParse(arr[i], out res))
							{
								istext[i] = true;
							}
						}
						dt.Rows.Add(dr);
					}
					counter++;
				}
			}
			DataTable dtCloned = dt.Clone();
			for (int i = 0; i < dtCloned.Columns.Count; i++)
			{
				dtCloned.Columns[i].DataType = istext[i] ? typeof(string) : typeof(double);
			}
			foreach (DataRow row in dt.Rows)
			{
				DataRow dr = dtCloned.NewRow();
				for (int i = 0; i < dtCloned.Columns.Count; i++)
				{
					if (dtCloned.Columns[i].DataType == typeof(double))
					{
						if (!string.IsNullOrEmpty(row[i].ToString()))
							dr[i] = Convert.ToDouble(row[i]);
					}
					else
						dr[i] = row[i];
				}
				dtCloned.Rows.Add(dr);
			}
			return dtCloned;
		}
		public DataTable GetOutput(string WebServer, string WebServerU, string WebServerP, string outputname, string otype = "", string oid = "")
		{
			string session = GetSession(WebServer, WebServerU, WebServerP);
			object ObjectManagerService = GetObjectManagerService(WebServer, WebServerU, WebServerP);
			MethodInfo mi = ObjectManagerService.GetType().GetMethod("GetGlobalReportID");
			object[] argss = new object[2] { session, outputname };
			string id = mi.Invoke(ObjectManagerService, argss).ToString();
			if (id.Equals(NullLongData.ToString()))
				throw new Exception($"Cannot find output: {outputname}");

			CorrectWebAddress(ref WebServer);
			string context = $"%3a450+{id}";
			if (!string.IsNullOrEmpty(otype) && !string.IsNullOrEmpty(oid))
			{
				context = $"%3a{otype}+{oid}";
			}
			string url = $"{WebServer}viewoutput.aspx?OutputID={id}&VT=16&Context={context}&SkipWait=1&ServiceSessionID={session}";

			Console.WriteLine(url);
			Uri ur = new Uri(url);
			HttpWebRequest request = (HttpWebRequest)WebRequest.Create(ur);
			request.Timeout = 600000;
			HttpWebResponse response = (HttpWebResponse)request.GetResponse();

			string res = "";
			if (response.StatusCode == HttpStatusCode.OK)
			{
				Stream receiveStream = response.GetResponseStream();
				StreamReader readStream = null;

				if (response.CharacterSet == null)
				{
					readStream = new StreamReader(receiveStream);
				}
				else
				{
					readStream = new StreamReader(receiveStream, Encoding.GetEncoding(response.CharacterSet));
				}

				res = readStream.ReadToEnd();
				response.Close();
				readStream.Close();
			}
			return BuildDataSet(res);
		}
		//objects loaders
		public DataTable LoadWells(string WebServer, string WebServerU, string WebServerP)
		{
			return GetOutput(WebServer, WebServerU, WebServerP, "PetrelSource");
		}
		public string GetWellID(string WebServer, string WebServerU, string WebServerP, string UWI)
		{
			string session = GetSession(WebServer, WebServerU, WebServerP);
			object ObjectManagerService = GetObjectManagerService(WebServer, WebServerU, WebServerP);
			MethodInfo mi = ObjectManagerService.GetType().GetMethod("GetWellID");
			object[] argss = new object[2] { session, UWI };
			string id = mi.Invoke(ObjectManagerService, argss).ToString();
			return id;
		}
		public DataTable LoadSurvey(string WebServer, string WebServerU, string WebServerP, string UWI)
		{
			string id = GetWellID(WebServer, WebServerU, WebServerP, UWI);
			return GetOutput(WebServer, WebServerU, WebServerP, "PetrelSourceSurvey", welltype.ToString(), id);
		}
		public DataTable LoadMonthlyProduction(string WebServer, string WebServerU, string WebServerP, string UWI)
		{
			string id = GetWellID(WebServer, WebServerU, WebServerP, UWI);
			return GetOutput(WebServer, WebServerU, WebServerP, "PetrelSourceProductionMonthly", welltype.ToString(), id);
		}
		public DataTable LoadDailyProduction(string WebServer, string WebServerU, string WebServerP, string UWI)
		{
			string id = GetWellID(WebServer, WebServerU, WebServerP, UWI);
			return GetOutput(WebServer, WebServerU, WebServerP, "PetrelSourceProductionDaily", welltype.ToString(), id);
		}
        //uploads
        bool PrepareZippedArray(string strInFile, out byte[] buf)
        {
            buf = null;
            string strFileName = Path.GetFileName(strInFile);
            FileInfo fi = new FileInfo(strInFile);

            MemoryStream ms = null;
            try
            {
                ms = new MemoryStream();
                ZipOutputStream zipStream = new ZipOutputStream(ms);
                zipStream.SetLevel(9);
                ZipEntry newEntry = new ZipEntry(strFileName);
                newEntry.DateTime = fi.LastWriteTime;
                newEntry.Size = fi.Length;
                zipStream.PutNextEntry(newEntry);

                byte[] buffer = new byte[4096];
                FileStream streamReader = File.OpenRead(strInFile);
                StreamUtils.Copy(streamReader, zipStream, buffer);

                streamReader.Close();
                zipStream.CloseEntry();
                zipStream.IsStreamOwner = false;
                zipStream.Close();

                buf = ms.ToArray();
                return true;
            }
            catch (System.Exception /*ex*/)
            {
                return false;
            }
            finally
            {
                if (ms != null)
                    ms.Close();
            }
        }
        object GetMigproSerice(string WebServer, string WebServerU, string WebServerP)
        {
            if (string.IsNullOrEmpty(WebServer))
                throw new Exception("Invalid web server address!");
            //store all instances of service class as each instance is a separate dll and loaded each time into memory
            string hash = "Migration" + Webhash(WebServer, WebServerU, WebServerP);
            if (WebServices.ContainsKey(hash))
            {
                return WebServices[hash];
            }

            ServiceDescriptionImporter importer = new ServiceDescriptionImporter();
            using (System.Net.WebClient client = new System.Net.WebClient())
            {
                CorrectWebAddress(ref WebServer);

                System.IO.Stream stream = client.OpenRead(WebServer + MigImportServiceName + ".asmx?wsdl");
                importer.ProtocolName = "Soap12";
                ServiceDescription description = ServiceDescription.Read(stream);
                importer.AddServiceDescription(description, null, null);
                importer.Style = ServiceDescriptionImportStyle.Client;
            }

            importer.CodeGenerationOptions = System.Xml.Serialization.CodeGenerationOptions.GenerateProperties;
            CodeNamespace nmspace = new CodeNamespace();
            NameSpaceCounter++;
            CodeCompileUnit CompileUnit = new CodeCompileUnit();
            CompileUnit.Namespaces.Add(nmspace);
            ServiceDescriptionImportWarnings warning = importer.Import(nmspace, CompileUnit);
            if (warning == 0)
            {
                string[] assemblyReferences = new string[2] { "System.Web.Services.dll", "System.Xml.dll" };
                CompilerParameters parms = new CompilerParameters(assemblyReferences);
                CompilerResults results;
                using (CodeDomProvider DomProvider = CodeDomProvider.CreateProvider("CSharp"))
                {
                    results = DomProvider.CompileAssemblyFromDom(parms, CompileUnit);
                }

                object wsvcClass = results.CompiledAssembly.CreateInstance(MigImportServiceName);
                WebServices[hash] = wsvcClass;
                return wsvcClass;
            }
            throw new Exception(message: "Failed to connect to web servie. Check web server exists.");

        }
        public int UploadData(string WebServer, string WebServerU, string WebServerP, string datafile, string migfile)
        {
            object Service = GetMigproSerice(WebServer, WebServerU, WebServerP);
            MethodInfo mi = Service.GetType().GetMethod("ImportFileToFieldpro");
            GetCorrectedUserCreds(ref WebServerU, ref WebServerP);

            byte[] b1 = null;
            PrepareZippedArray(datafile, out b1);
            byte[] b2 = null;
            PrepareZippedArray(migfile, out b2);

            object[] argss = new object[7] { WebServerU, WebServerP, Path.GetFileName(datafile), b1, Path.GetFileName(migfile), b2, 3};
            return Convert.ToInt32(mi.Invoke(Service, argss));
        }
    }
}