using System.Diagnostics;
using System.IO;
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

                if (Debugger.IsAttached)
                {
                    try
                    {
                        var traceOutput = HostingEnvironment.MapPath("~/traceOutput.log");
                        if (File.Exists(traceOutput))
                            File.Delete(traceOutput);

                        var listener = new TextWriterTraceListener(traceOutput, "StartupListener");

                        Trace.Listeners.Add(listener);
                        Trace.AutoFlush = true;
                    }
                    catch { }
                }

                var cfg = BootstrapperSection.Initialize();


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

                //todo: implementar dependência entre módulos

                if (cfg.Kompiler.Enabled)
                {
                    KompilerEntryPoint.AddReferences(typeof(Controller), typeof(WebPageRenderingBase), typeof(WebCacheWrapper), typeof(ViewRenderer), typeof(DbToLocal), typeof(CustomErrorHttpModule.ErrorModel));

                    using (DisposableTimer.StartNew("Kompiler"))
                    {
                        KompilerEntryPoint.Execute();
                    }
                }

                //config routing
                //var routes = RouteTable.Routes;

                //if (EntropiaSection.Instance.InsertRoutesDefaults)
                //{
                //    routes.RouteExistingFiles = false;
                //    routes.LowercaseUrls = true;
                //    routes.AppendTrailingSlash = true;

                //    routes.IgnoreRoute("{resource}.axd/{*pathInfo}");
                //    routes.IgnoreRoute("{*favicon}", new { favicon = @"(.*/)?favicon.ico(/.*)?" });
                //    routes.IgnoreRoute("{*staticfile}", new { staticfile = @".*\.(css|js|txt|png|gif|jpg|jpeg|bmp)(/.*)?" });

                //    routes.IgnoreRoute("Content/{*pathInfo}");
                //    routes.IgnoreRoute("Scripts/{*pathInfo}");
                //    routes.IgnoreRoute("Bundles/{*pathInfo}");
                //}

                //if (EntropiaSection.Instance.EnableDumpLog)
                //{
                //    var endpoint = EntropiaSection.Instance.DumpLogEndPoint;
                //    routes.MapHttpHandler<DumpHandler>(endpoint);
                //}
            }
        }

        public static void PostStart()
        {
            if (_initialized)
                return;

            _initialized = true;

            using (DisposableTimer.StartNew("RUNNING POST_START ..."))
            {
                if (BootstrapperSection.Instance.MvcTrace.Enabled)
                {
                    GlobalFilters.Filters.Add(new MvcTracerFilter());
                }

                var application = HttpContext.Current.ApplicationInstance;

                var modules = application.Modules;
                foreach (var module in modules)
                {
                    Trace.TraceInformation("Module Loaded: {0}", module);
                }

                //dump routes
                var routes = RouteTable.Routes;

                var i = routes.Count;
                Trace.TraceInformation("Found {0} routes in RouteTable", i);

                foreach (var routeBase in routes)
                {
                    var route = (Route)routeBase;
                    Trace.TraceInformation("Handler: {0} at URL: {1}", route.RouteHandler, route.Url);
                }

                if (!Debugger.IsAttached)
                {
                    Trace.Listeners.Remove("StartupListener");
                }
            }
        }
    }
}