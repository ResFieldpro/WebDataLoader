using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebDataLoader
{
	public class WebImporter
	{
		string WebSession(string WebServer, string WebServerU, string WebServerP)
		{
			string newhash = WebServer + WebServerU + WebServerP;
			if (newhash != webhash)//not initialized or settings were changed
			{
				webhash = newhash;
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

				ServiceDescriptionImporter importer = new ServiceDescriptionImporter();
				using (System.Net.WebClient client = new System.Net.WebClient())
				{
					System.IO.Stream stream = client.OpenRead(WebServer + "ObjectManagerService.asmx?wsdl");
					ServiceDescription description = ServiceDescription.Read(stream);
					importer.ProtocolName = "Soap12";
					importer.AddServiceDescription(description, null, null);
					importer.Style = ServiceDescriptionImportStyle.Client;
				}

				importer.CodeGenerationOptions = System.Xml.Serialization.CodeGenerationOptions.GenerateProperties;
				CodeNamespace nmspace = new CodeNamespace();
				CodeCompileUnit unit1 = new CodeCompileUnit();
				unit1.Namespaces.Add(nmspace);
				ServiceDescriptionImportWarnings warning = importer.Import(nmspace, unit1);
				if (warning == 0)
				{
					string[] assemblyReferences = new string[2] { "System.Web.Services.dll", "System.Xml.dll" };
					CompilerParameters parms = new CompilerParameters(assemblyReferences);
					CompilerResults results;
					using (CodeDomProvider provider1 = CodeDomProvider.CreateProvider("CSharp"))
					{
						results = provider1.CompileAssemblyFromDom(parms, unit1);
					}

					object wsvcClass = results.CompiledAssembly.CreateInstance("ObjectManagerService");


					MethodInfo mi = wsvcClass.GetType().GetMethod("Login");

					//prepare parameters
					object[] argss = new object[2] { WebServerU, WebServerP };

					websession = mi.Invoke(wsvcClass, argss).ToString();
				}
			}
			return websession;
		}

	}
}
