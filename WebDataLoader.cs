using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Web.Services.Description;

namespace WebDataLoader
{
	public class WebImporter
	{
		static string ObjectManagerServiceName = "ObjectManagerService";
		static Dictionary<string, object> WebServices = new Dictionary<string, object>();
		static Dictionary<string, string> WebSessions = new Dictionary<string, string>();
		string Webhash(string WebServer, string WebServerU, string WebServerP)
		{
			return "server:" + WebServer + "user:" + WebServerU + "password:" + WebServerP;
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
					res += b.ToString("x2");
				WebServerU = "WINLGN:" + res;
			}
		}
		object GetObjectManagerService(string WebServer, string WebServerU, string WebServerP)
		{
			//store all instances of service class as each instance is a separate dll and loaded each time into memory
			string hash = Webhash(WebServer, WebServerU, WebServerP);
			if (WebServices.ContainsKey(hash))
			{
				return WebServices[hash];
			}

			ServiceDescriptionImporter importer = new ServiceDescriptionImporter();
			using (System.Net.WebClient client = new System.Net.WebClient())
			{
				System.IO.Stream stream = client.OpenRead(WebServer + ObjectManagerServiceName + ".asmx?wsdl");
				ServiceDescription description = ServiceDescription.Read(stream);
				importer.ProtocolName = "Soap12";
				importer.AddServiceDescription(description, null, null);
				importer.Style = ServiceDescriptionImportStyle.Client;
			}

			importer.CodeGenerationOptions = System.Xml.Serialization.CodeGenerationOptions.GenerateProperties;
			CodeNamespace nmspace = new CodeNamespace();
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
			throw new Exception("Failed to connect to web servie. Check web server exists.");
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
	}
}
