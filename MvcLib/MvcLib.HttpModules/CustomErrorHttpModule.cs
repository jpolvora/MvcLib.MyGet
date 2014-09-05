using System;
using System.Diagnostics;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using MvcLib.Common;
using MvcLib.Common.Configuration;
using MvcLib.Common.Mvc;

namespace MvcLib.HttpModules
{
    public class CustomErrorHttpModule : IHttpModule
    {
        private string _errorViewPath;
        private string _errorController;

        public void Init(HttpApplication context)
        {
            context.BeginRequest += OnBeginRequest;
            context.Error += OnError;

            _errorViewPath = BootstrapperSection.Instance.HttpModules.CustomError.ErrorViewPath;
            _errorController = BootstrapperSection.Instance.HttpModules.CustomError.ControllerName;
        }

        static void OnBeginRequest(object sender, EventArgs eventArgs)
        {
            var application = (HttpApplication)sender;
            application.Response.TrySkipIisCustomErrors = true;
        }

        void OnError(object sender, EventArgs args)
        {
            var application = (HttpApplication)sender;

            var server = application.Server;
            var response = application.Response;
            var request = application.Request;
            var exception = server.GetLastError();

            if (exception == null)
            {
                Trace.TraceWarning("Erro desconhecido. Url: {0}", request.Url);
                return;
            }

            Trace.TraceInformation("[CustomError]: Ocorreu uma exceção: {0}", exception.Message);

            var statusCode = response.StatusCode;
            var httpException = exception as HttpException;
            if (httpException != null)
            {
                statusCode = httpException.GetHttpCode();
            }

            server.ClearError();
            response.Clear();
            response.StatusCode = statusCode;
            response.StatusDescription = exception.Message;

            //Prevents customError behavior when the request is determined to be an AJAX request.
            if (request.IsAjaxRequest())
            {
                response.Write(string.Format("<html><body><h1>{0} {1}</h1></body></html>", statusCode, exception.Message));
            }
            else
            {
                var model = new ErrorModel()
                {
                    Message = exception.Message,
                    FullMessage = exception.LoopException(),
                    StackTrace = exception.StackTrace,
                    Url = application.Request.RawUrl,
                    StatusCode = statusCode
                };

                bool useController = !string.IsNullOrWhiteSpace(_errorController);
                if (useController)
                {
                    RenderController(application.Context, _errorController, model);
                }
                else
                {
                    RenderView(_errorViewPath, model, response);
                }
            }

            Trace.TraceInformation("[CustomError]: Completing response.");

            if (statusCode == 500)
            {
                LogEvent.Raise(exception.Message, exception);
            }

            //application.CompleteRequest(); //não imprime o resultado
            response.End();
        }

        private static void RenderView(string errorViewPath, ErrorModel model, HttpResponse response)
        {
            try
            {
                var html = ViewRenderer.RenderView(errorViewPath, model);
                response.Write(html);

            }
            catch (Exception ex)
            {
                var msg = string.Format("Error rendering view '{0}': {1}", errorViewPath, ex.Message);
                response.Write(msg);
                response.Write("<BR />");
                response.Write(model.Message);
                response.StatusCode = 500;
                Trace.TraceInformation(msg);
            }
        }

        private static void RenderController(HttpContext context, string controllerName, ErrorModel model)
        {
            var wrapper = new HttpContextWrapper(context);

            var routeData = new RouteData();
            routeData.Values.Add("controller", controllerName);
			routeData.Values.Add("action", "Error");
            routeData.Values.Add("Message", model.Message);
            routeData.Values.Add("StackTrace", model.StackTrace);
            routeData.Values.Add("Url", model.Url);
            routeData.Values.Add("StatusCode", model.StatusCode);

            var factory = ControllerBuilder.Current.GetControllerFactory();

            IController controller = null;
            try
            {
                var requestContext = new RequestContext(wrapper, routeData);
                controller = factory.CreateController(requestContext, controllerName);
                if (controller != null)
                {

                    controller.Execute(requestContext);
                }
            }
            catch (Exception ex)
            {
                var msg = string.Format("Error executing controller {0}: {1}", controller, ex.Message);
                context.Response.Write(msg);
                context.Response.Write("<BR />");
                context.Response.Write(model.Message);
                context.Response.StatusCode = 500;
                Trace.TraceInformation(msg);
            }
            finally
            {
                if (controller != null)
                {
                    factory.ReleaseController(controller);
                }
            }
        }

        public void Dispose()
        {
        }

        public class ErrorModel
        {
            public string Message { get; set; }
            public string FullMessage { get; set; }
            public string StackTrace { get; set; }
            public string Url { get; set; }
            public int StatusCode { get; set; }

            public override string ToString()
            {
                return string.Format("{0} - {1}", Message, Url);
            }
        }
    }
}