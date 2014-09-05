using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web.Hosting;
using System.Xml.Linq;
using MvcLib.Common.Configuration;
using MvcLib.DbFileSystem;

namespace MvcLib.PluginLoader
{
    /*
     * http://shazwazza.com/post/Developing-a-plugin-framework-in-ASPNET-with-medium-trust
     */

    public class PluginLoaderEntryPoint
    {
        public static readonly DirectoryInfo PluginFolder;

        private static bool _initialized;

        static PluginLoaderEntryPoint()
        {
            //determinar probingPath

            var privatePath = "~/_plugins";

            try
            {
                var configFile = XElement.Load(AppDomain.CurrentDomain.SetupInformation.ConfigurationFile);

                var probingElement = configFile.Descendants("runtime")
                    .SelectMany(runtime => runtime.Elements(XName.Get("probing")))
                    .FirstOrDefault();

                if (probingElement != null)
                {
                    privatePath = probingElement.Attribute("privatePath").Value;
                }
            }
            catch (Exception ex)
            {
                Trace.TraceInformation("Error reading probing privatePath in web.config. {0}", ex.Message);
            }

            PluginFolder = new DirectoryInfo(HostingEnvironment.MapPath(privatePath));

            if (!PluginFolder.Exists)
            {
                PluginFolder.Create();
            }
        }

        public static void Initialize()
        {
            if (_initialized)
                return;

            _initialized = true;

            if (AppDomain.CurrentDomain.IsFullyTrusted)
            {
                AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
            }
            else
            {
                Trace.TraceWarning("We are not in FULL TRUST! We must use private probing path in Web.Config");
            }

            AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;

            var existingAssemblies = PluginFolder.GetFiles("*.dll", SearchOption.AllDirectories);
            var filenames = existingAssemblies.Select(fileInfo => fileInfo.FullName).ToList();

            if (BootstrapperSection.Instance.PluginLoader.DeleteFiles)
            {
                foreach (var filename in filenames)
                {
                    if (File.Exists(filename))
                        File.Delete(filename);
                }

                filenames.Clear();
            }


            if (BootstrapperSection.Instance.PluginLoader.LoadFromDb)
            {
                var assemblies = LoadFromDb();

                filenames.AddRange(WriteToDisk(assemblies));
            }

            LoadAndRegisterPlugins(filenames);
        }


        public static void LoadPlugin(string fileName, byte[] bytes)
        {
            Trace.TraceInformation("Writing and Loading new compiled assembly: '{0}'", fileName);
            var kvp = new KeyValuePair<string, byte[]>(fileName, bytes);

            var fileNames = WriteToDisk(new[] { kvp });
            LoadAndRegisterPlugins(fileNames);
        }

        static Dictionary<string, byte[]> LoadFromDb()
        {
            var assemblies = new Dictionary<string, byte[]>();
            using (var ctx = new DbFileContext())
            {
                var files = ctx.DbFiles
                    .Where(x => !x.IsHidden && !x.IsDirectory && x.IsBinary && x.Extension.Equals(".dll"))
                    .ToList();

                foreach (var s in files)
                {
                    Trace.TraceInformation("[PluginLoader]: Found assembly from Database: {0}", s.VirtualPath);
                    assemblies.Add(s.Name, s.Bytes);
                }
            }

            return assemblies;
        }

        static IEnumerable<string> WriteToDisk(IEnumerable<KeyValuePair<string, byte[]>> assemblies)
        {
            var result = new List<string>();
            try
            {
                foreach (var assembly in assemblies)
                {
                    var fileName = assembly.Key;
                    if (!Path.HasExtension(assembly.Key))
                        fileName = assembly.Key + ".dll";

                    var fullFileName = Path.Combine(PluginFolder.FullName, fileName);

                    if (File.Exists(fullFileName))
                        File.Delete(fullFileName);

                    File.WriteAllBytes(fullFileName, assembly.Value);

                    result.Add(fullFileName);
                }
            }
            catch (Exception ex)
            {
                Trace.TraceInformation(ex.Message);
            }

            return result;
        }

        static void LoadAndRegisterPlugins(IEnumerable<string> fileNames)
        {
            foreach (var fileName in fileNames)
            {
                if (File.Exists(fileName))
                {
                    PluginStorage.Register(fileName);
                }
            }
        }

        private static Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            if (args.RequestingAssembly != null)
                return args.RequestingAssembly;

            var ass = PluginStorage.FindAssembly(args.Name);
            if (ass != null)
                Trace.TraceInformation("Assembly found and resolved: {0} = {1}", ass.FullName, ass.Location);

            return ass; //not found
        }

        private static void OnAssemblyLoad(object sender, AssemblyLoadEventArgs args)
        {
            if (args.LoadedAssembly.GlobalAssemblyCache)
                return;

            Trace.TraceInformation("Assembly Loaded... {0}", args.LoadedAssembly.Location);

            var types = args.LoadedAssembly.GetExportedTypes();

            if (types.Any())
            {
                foreach (var type in types)
                {
                    Trace.TraceInformation("Type exported: {0}", type.FullName);
                }
            }
            else
            {
                Trace.TraceInformation("No types exported by Assembly: '{0}'", args.LoadedAssembly.GetName().Name);
            }

            if (args.LoadedAssembly.IsDynamic)
            {
                Trace.TraceInformation("DYNAMIC Assembly Loaded... {0}", args.LoadedAssembly.Location);
                return;
            }

            var path = Path.GetDirectoryName(args.LoadedAssembly.Location);

            if (string.IsNullOrWhiteSpace(path) ||
                !path.StartsWith(PluginFolder.FullName, StringComparison.InvariantCultureIgnoreCase)) return;

            try
            {
                PluginStorage.Register(args.LoadedAssembly);
            }
            catch (Exception ex)
            {
                Trace.TraceInformation(ex.Message);
            }
        }

        //public static void RecursiveDeleteDirectory(DirectoryInfo baseDir, bool self, params string[] extensions)
        //{
        //    if (!baseDir.Exists)
        //        return;

        //    foreach (var directoryInfo in baseDir.EnumerateDirectories())
        //    {
        //        RecursiveDeleteDirectory(directoryInfo, true, extensions);
        //    }

        //    if (self && baseDir.Exists)
        //        baseDir.Delete(true);
        //}

        //public static bool IsFileLocked(string path)
        //{
        //    if (!File.Exists(path))
        //        return false;

        //    FileStream file = null;
        //    try
        //    {
        //        file = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
        //    }
        //    catch (Exception)
        //    {
        //        return true;
        //    }
        //    finally
        //    {
        //        if (file != null)
        //            file.Close();
        //    }

        //    return false;
        //}
    }
}