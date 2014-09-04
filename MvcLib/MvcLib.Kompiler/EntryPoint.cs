using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using MvcLib.Common;
using Roslyn.Compilers;

namespace MvcLib.Kompiler
{
    public class EntryPoint
    {
        internal const string CompiledAssemblyName = "db-compiled-assembly";

        private static bool _initialized;

        public static void Execute()
        {
            if (_initialized)
                throw new InvalidOperationException("Compilador só pode ser executado no Pre-Start!");

            _initialized = true;

            using (DisposableTimer.StartNew("Dynamic Compilation"))
            {
                byte[] buffer = new byte[0];
                string msg;

                try
                {
                    //todo: usar depdendency injection
                    IKompiler kompiler;

                    if (Config.ValueOrDefault("Kompiler:UseRoslyn", false))
                    {
                        kompiler = new RoslynWrapper();
                    }
                    else
                    {
                        kompiler = new CodeDomWrapper();
                    }

                    if (Config.ValueOrDefault("Kompiler:LoadFromDb", false))
                    {
                        Trace.TraceInformation("Compiling from DB...");
                        var source = KompilerDbService.LoadSourceCodeFromDb();
                        msg = kompiler.CompileFromSource(source, out buffer);
                    }
                    else
                    {
                        var localRootFolder = Config.ValueOrDefault("DumpToLocalFolder", "~/dbfiles");
                        Trace.TraceInformation("Compiling from Local File System: {0}", localRootFolder);
                        msg = kompiler.CompileFromFolder(localRootFolder, out buffer);
                    }
                }
                catch (Exception ex)
                {
                    msg = ex.Message;
                    Trace.TraceInformation("Erro durante a compilação do projeto no banco de dados. \r\n" + ex.Message);
                }

                if (string.IsNullOrWhiteSpace(msg) && buffer.Length > 0)
                {
                    Trace.TraceInformation("[Kompiler]: DB Compilation Result: SUCCESS");

                    if (!Config.ValueOrDefault("Kompiler:ForceRecompilation", false))
                    {
                        //só salva no banco se compilação forçada for False
                        KompilerDbService.SaveCompiledCustomAssembly(CompiledAssemblyName, buffer);
                    }

                    PluginLoader.EntryPoint.LoadPlugin(CompiledAssemblyName + ".dll", buffer);
                }
                else
                {
                    Trace.TraceInformation("[Kompiler]: DB Compilation Result: Bytes:{0}, Msg:{1}",
                        buffer.Length, msg);
                }
            }
        }

        public static void AddReferences(params Type[] types)
        {
            if (_initialized)
                throw new InvalidOperationException("Compilador só pode ser executado no Pre-Start!");

            foreach (var type in types)
            {
                CodeDomReferences.Add(type.Assembly.Location);
                RoslynReferences.Add(new MetadataFileReference(type.Assembly.Location));
            }
        }

        public static void AddReferences(params Assembly[] assemblies)
        {
            foreach (var assembly in assemblies)
            {
                RoslynReferences.Add(new MetadataFileReference(assembly.Location));
            }
        }

        //todo: passar para a classe correta
        internal static List<string> CodeDomReferences = new List<string>()
        {
                    typeof (object).Assembly.Location, //System
                    typeof (Enumerable).Assembly.Location, //System.Core.dll
                    typeof(System.Data.DbType).Assembly.Location, //System.Data         
                    typeof(Microsoft.CSharp.RuntimeBinder.Binder).Assembly.Location, //Microsoft.CSharp
                    typeof(System.Web.HttpApplication).Assembly.Location,
                    typeof(System.ComponentModel.DataAnnotations.DataType).Assembly.Location,
                    typeof(DbContext).Assembly.Location,
                    typeof(CodeDomWrapper).Assembly.Location
        };

        //todo: passar para a classe correta
        internal static List<MetadataReference> RoslynReferences = new List<MetadataReference>
        {
            MetadataReference. CreateAssemblyReference("mscorlib"),
            MetadataReference.CreateAssemblyReference("System"),
            MetadataReference.CreateAssemblyReference("System.Core"),
            MetadataReference.CreateAssemblyReference("System.Data"),
            MetadataReference.CreateAssemblyReference("Microsoft.CSharp"),
            MetadataReference.CreateAssemblyReference("System.Web"),
            MetadataReference.CreateAssemblyReference("System.ComponentModel.DataAnnotations"),
            new MetadataFileReference(typeof (Roslyn.Services.Solution).Assembly.Location), //self
            new MetadataFileReference(typeof (Roslyn.Compilers.CSharp.Compilation).Assembly.Location), //self
            new MetadataFileReference(typeof (Roslyn.Compilers.Common.CommonCompilation).Assembly.Location), //self
            new MetadataFileReference(typeof (Roslyn.Scripting.Session).Assembly.Location), //self
            new MetadataFileReference(typeof (RoslynWrapper).Assembly.Location), //self            
            new MetadataFileReference(typeof (DbContext).Assembly.Location), //ef    
        };

    }
}