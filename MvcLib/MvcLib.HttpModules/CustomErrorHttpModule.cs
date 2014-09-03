using System;
using System.Diagnostics;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using MvcLib.Common;
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

            _errorViewPath = Config.ValueOrDefault("CustomErrorViewPath", "~/views/shared/customerror.cshtml");
            _errorController = Config.ValueOrDefault("CustomErrorController", "");
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
<<<<<<< HEAD
<<<<<<< HEAD
            var exception = server.GetLastError();

=======
            var request = application.Request;
            var exception = server.GetLastError();

=======
            var request = application.Request;
            var exception = server.GetLastError();

>>>>>>> 5fa1022bb00c6386a1fb9a2ff8c149a6545ff7f0
            if (exception == null)
            {
                Trace.TraceWarning("Erro desconhecido. Url: {0}", request.Url);
                return;
            }

<<<<<<< HEAD
<<<<<<< HEAD
>>>>>>> f4d39c499476591d8185ca9a77b7d53b612692b3
=======
>>>>>>> 5fa1022bb00c6386a1fb9a2ff8c149a6545ff7f0
=======
            Trace.TraceError("[CustomError]: Ocorreu uma exceção: {0}", exception.Message);

>>>>>>> 9ec8d271c7c9e33ba29c606f6c8a96ba9a0f331d
            var statusCode = response.StatusCode;
            var httpException = exception as HttpException;
            if (httpException != null)
            {
                statusCode = httpException.GetHttpCode();
            }

<<<<<<< HEAD
<<<<<<< HEAD
            if (application.Context.Handler != null)
            {
                Trace.TraceError("[CustomErrorHttpModule]: Handler is {0}. Status is {1}", application.Context.Handler, statusCode);
                var handler = application.Context.Handler.GetType().Name;
                if (handler.Equals("StaticFileHandler", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

=======
>>>>>>> 5fa1022bb00c6386a1fb9a2ff8c149a6545ff7f0
            server.ClearError();
            response.ClearContent();
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

<<<<<<< HEAD
            Trace.TraceInformation("[CustomError]: Will end response now (avoid Transfer Handler)");
            response.StatusCode = statusCode;
=======
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
>>>>>>> f4d39c499476591d8185ca9a77b7d53b612692b3
=======
            Trace.TraceInformation("[CustomError]: Completing response.");
>>>>>>> 5fa1022bb00c6386a1fb9a2ff8c149a6545ff7f0

            if (statusCode == 500)
            {
                LogEvent.Raise(exception.Message, exception);
            }

            application.CompleteRequest(); //não imprime o resultado
            //response.End();
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
                Trace.TraceError(msg);
            }
        }

        private static void RenderController(HttpContext context, string controllerName, ErrorModel model)
        {
            var wrapper = new HttpContextWrapper(context);

            var routeData = new RouteData();
            routeData.Values.Add("controller", controllerName);
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
                Trace.TraceError(msg);
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