using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web;
using System.Web.Hosting;
using System.Web.Mvc;
using System.Web.Routing;
using System.Web.WebPages;
using Microsoft.Web.Infrastructure.DynamicModuleHelper;
using MvcLib.Bootstrapper;
using MvcLib.Common;
using MvcLib.Common.Cache;
using MvcLib.Common.Configuration;
using MvcLib.Common.Mvc;
using MvcLib.CustomVPP;
using MvcLib.CustomVPP.Impl;
using MvcLib.CustomVPP.RemapperVpp;
using MvcLib.DbFileSystem;
using MvcLib.FsDump;
using MvcLib.HttpModules;
using MvcLib.Kompiler;
using MvcLib.PluginLoader;

[assembly: WebActivatorEx.PreApplicationStartMethod(typeof(Init), "PreStart")]
[assembly: WebActivatorEx.PostApplicationStartMethod(typeof(Init), "PostStart")]

namespace MvcLib.Bootstrapper
{
    public class Init
    {
        private static bool _initialized;

        public static void PreStart()
        {
            using (DisposableTimer.StartNew("PRE_START"))
            {
                var executingAssembly = Assembly.GetExecutingAssembly();
                Trace.TraceInformation("Entry Assembly: {0}", executingAssembly.GetName().Name);

                var cfg = BootstrapperSection.Initialize();

                if (Debugger.IsAttached || cfg.TraceOutput.IsNotNullOrWhiteSpace())
                {
                    try
                    {
                        var path = cfg.TraceOutput;
                        if (!path.StartsWith("~"))
                            path = "~" + path;
                        var traceOutput = HostingEnvironment.MapPath(path);
                        if (File.Exists(traceOutput))
                            File.Delete(traceOutput);

                        var listener = new TextWriterTraceListener(traceOutput, "StartupListener");

                        Trace.Listeners.Add(listener);
                        Trace.AutoFlush = true;
                    }
                    catch { }
                }

                if (cfg.HttpModules.Trace.Enabled)
                {
                    DynamicModuleUtility.RegisterModule(typeof(TracerHttpModule));
                }

                if (cfg.StopMonitoring)
                {
                    HttpInternals.StopFileMonitoring();
                }

                if (cfg.HttpModules.CustomError.Enabled)
                {
                    DynamicModuleUtility.RegisterModule(typeof(CustomErrorHttpModule));
                }

                if (cfg.HttpModules.WhiteSpace.Enabled)
                {
                    DynamicModuleUtility.RegisterModule(typeof(WhitespaceModule));
                }

                using (DisposableTimer.StartNew("DbFileContext"))
                {
                    DbFileContext.Initialize();
                }

                if (cfg.PluginLoader.Enabled)
                {
                    using (DisposableTimer.StartNew("PluginLoader"))
                    {
                        PluginLoaderEntryPoint.Initialize();
                    }
                }

                if (cfg.VirtualPathProviders.SubFolderVpp.Enabled)
                {
                    SubfolderVpp.SelfRegister();
                }

                if (cfg.DumpToLocal.Enabled)
                {
                    using (DisposableTimer.StartNew("DumpToLocal"))
                    {
                        DbToLocal.Execute();
                    }
                }

                //todo: Dependency Injection
                if (cfg.VirtualPathProviders.DbFileSystemVpp.Enabled)
                {
                    var customvpp = new CustomVirtualPathProvider()
                        .AddImpl(new CachedDbServiceFileSystemProvider(new DefaultDbService(), new WebCacheWrapper()));
                    HostingEnvironment.RegisterVirtualPathProvider(customvpp);
                }

                KompilerEntryPoint.AddReferences(
                    typeof(Controller),
                    typeof(WebPageRenderingBase),
                    typeof(WebCacheWrapper),
                    typeof(ViewRenderer),
                    typeof(DbToLocal),
                    typeof(CustomErrorHttpModule.ErrorModel));

                if (cfg.Kompiler.Enabled)
                {
                    using (DisposableTimer.StartNew("Kompiler"))
                    {
                        KompilerEntryPoint.Execute();
                    }
                }

                if (cfg.InsertRoutes)
                {
                    var routes = RouteTable.Routes;

                    routes.RouteExistingFiles = false;
                    routes.LowercaseUrls = true;
                    routes.AppendTrailingSlash = true;

                    routes.IgnoreRoute("{resource}.axd/{*pathInfo}");
                    routes.IgnoreRoute("{*favicon}", new { favicon = @"(.*/)?favicon.ico(/.*)?" });
                    routes.IgnoreRoute("{*staticfile}", new { staticfile = @".*\.(css|js|txt|png|gif|jpg|jpeg|bmp)(/.*)?" });

                    routes.IgnoreRoute("Content/{*pathInfo}");
                    routes.IgnoreRoute("Scripts/{*pathInfo}");
                    routes.IgnoreRoute("Bundles/{*pathInfo}");

                    //routes.MapRoute("MvcLib", "{controller}/{action}", new string[] { "" });


                    if (cfg.TraceOutput.IsNotNullOrWhiteSpace())
                    {
                        //routes.MapHttpHandler<WebPagesRouteHandler>("~/dump.cshtml");
                    }
                }
            }
        }

        public static void PostStart()
        {
            if (_initialized)
                return;

            _initialized = true;

            using (DisposableTimer.StartNew("RUNNING POST_START ..."))
            {
                var cfg = BootstrapperSection.Instance;

                if (cfg.MvcTrace.Enabled)
                {
                    GlobalFilters.Filters.Add(new MvcTracerFilter());
                }

                if (cfg.Verbose)
                {
                    var application = HttpContext.Current.ApplicationInstance;

                    var modules = application.Modules;
                    foreach (var module in modules)
                    {
                        Trace.TraceInformation("Module Loaded: {0}", module);
                    }
                }

                //dump routes
                var routes = RouteTable.Routes;

                if (cfg.Verbose)
                {
                    var i = routes.Count;
                    Trace.TraceInformation("Found {0} routes in RouteTable", i);

                    foreach (var routeBase in routes)
                    {
                        var route = (Route) routeBase;
                        Trace.TraceInformation("Handler: {0} at URL: {1}", route.RouteHandler, route.Url);
                    }
                }

                if (!Debugger.IsAttached)
                {
                    Trace.Listeners.Remove("StartupListener");
                }

                //viewengine locations
                var mvcroot = cfg.DumpToLocal.Folder;

                var razorViewEngine = ViewEngines.Engines.OfType<RazorViewEngine>().FirstOrDefault();
                if (razorViewEngine != null)
                {
                    Trace.TraceInformation("Configuring RazorViewEngine Location Formats");
                    var vlf = new string[]
                    {
                        mvcroot + "/Views/{1}/{0}.cshtml",
                        mvcroot + "/Views/Shared/{0}.cshtml",
                    };
                    razorViewEngine.ViewLocationFormats = razorViewEngine.ViewLocationFormats.Extend(false, vlf);

                    var mlf = new string[]
                    {
                        mvcroot + "/Views/{1}/{0}.cshtml",
                        mvcroot + "/Views/Shared/{0}.cshtml",
                    };
                    razorViewEngine.MasterLocationFormats = razorViewEngine.MasterLocationFormats.Extend(false, mlf);

                    var plf = new string[]
                    {
                        mvcroot + "/Views/{1}/{0}.cshtml",
                        mvcroot + "/Views/Shared/{0}.cshtml",
                    };
                    razorViewEngine.PartialViewLocationFormats = razorViewEngine.PartialViewLocationFormats.Extend(false, plf);

                    var avlf = new string[]
                    {
                        mvcroot + "/Areas/{2}/Views/{1}/{0}.cshtml",
                        mvcroot + "/Areas/{2}/Views/Shared/{0}.cshtml",
                    };
                    razorViewEngine.AreaViewLocationFormats = razorViewEngine.AreaViewLocationFormats.Extend(false, avlf);

                    var amlf = new string[]
                    {
                        mvcroot + "/Areas/{2}/Views/{1}/{0}.cshtml",
                        mvcroot + "/Areas/{2}/Views/Shared/{0}.cshtml",
                    };
                    razorViewEngine.AreaMasterLocationFormats = razorViewEngine.AreaMasterLocationFormats.Extend(false, amlf);

                    var apvlf = new string[]
                    {
                        mvcroot + "/Areas/{2}/Views/{1}/{0}.cshtml",
                        mvcroot + "/Areas/{2}/Views/Shared/{0}.cshtml",
                    };
                    razorViewEngine.AreaPartialViewLocationFormats = razorViewEngine.AreaPartialViewLocationFormats.Extend(false, apvlf);

                    if (cfg.Verbose)
                    {
                        foreach (var locationFormat in razorViewEngine.ViewLocationFormats)
                        {
                            Trace.WriteLine(locationFormat);
                        }
                    }

                    ViewEngines.Engines.Clear();
                    ViewEngines.Engines.Add(razorViewEngine);
                }
                else
                {
                    Trace.TraceInformation("Cannot Configure RazorViewEngine: View Engine not found");
                }
            }
        }
    }
}