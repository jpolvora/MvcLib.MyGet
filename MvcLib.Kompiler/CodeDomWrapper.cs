using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.CSharp;

namespace MvcLib.Kompiler
{
    public class CodeDomWrapper
    {
        public static string CompileFromStringArray(Dictionary<string, string> sourceFiles, out byte[] buffer)
        {
            if (sourceFiles == null)
                throw new ArgumentNullException("sourceFiles");

            CodeDomProvider codeDomProvider = new CSharpCodeProvider();
            var compilerParameters = new CompilerParameters
            {
                OutputAssembly = EntryPoint.CompiledAssemblyName + ".dll",
                GenerateExecutable = false,
                GenerateInMemory = true,
                IncludeDebugInformation = false,
                ReferencedAssemblies =
                {
                    typeof (object).Assembly.Location,
                    typeof (Enumerable).Assembly.Location
                }
            };
            var src = sourceFiles.Select(s => s.Value).ToArray();

            CompilerResults result = codeDomProvider.CompileAssemblyFromSource(compilerParameters, src);

            buffer = new byte[0];

            if (result.Errors.HasErrors)
            {
                var sb = new StringBuilder();
                foreach (CompilerError error in result.Errors)
                {
                    sb.AppendFormat("Erro {0}, {1}", error.FileName, error.ErrorText).AppendLine();
                }
                return sb.ToString();
            }

            var file = result.PathToAssembly;
            buffer = File.ReadAllBytes(file);

            return string.Empty;
        }
    }
}