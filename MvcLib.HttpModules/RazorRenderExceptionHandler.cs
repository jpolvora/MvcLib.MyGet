using System;
using System.Configuration;
using System.Diagnostics;
using System.Net.Mail;
using System.Web;
using MvcLib.Common;
using MvcLib.Common.Configuration;
using MvcLib.Common.Mvc;

namespace MvcLib.HttpModules
{
    public class RazorRenderExceptionHandler : ExceptionHandler<CustomException>
    {
        public RazorRenderExceptionHandler(HttpApplication application, string errorViewPath)
            : base(application, errorViewPath, LogActionEx)
        {
        }

        static void LogActionEx(HttpException exception)
        {
            var status = exception.GetHttpCode();
            if (status < 500) return;
            LogEvent.Raise(exception.Message, exception.GetBaseException());
            using (var smptClient = new SmtpClient())
            {
                //todo: Enviar async
                smptClient.Send("Admin", BootstrapperSection.Instance.Mail.MailDeveloper, "Exception " + status, exception.ToString());
            }
        }

        protected override bool IsProduction()
        {
            //checa se o ambiente é de produção
            bool release = ConfigurationManager.AppSettings["Environment"]
                .Equals("Release", StringComparison.OrdinalIgnoreCase);

            return release;
        }

        protected override void RenderCustomException(CustomException exception)
        {
            //Application.Context.RewritePath(ErrorViewPath);

            var model = new ErrorModel()
            {
                Message = exception.Message,
                FullMessage = exception.ToString(),
                StackTrace = exception.StackTrace,
                Url = Application.Request.RawUrl,
                StatusCode = Application.Response.StatusCode,
            };

            Trace.TraceWarning("Rendering razor view: {0}", ErrorViewPath);
            var html = ViewRenderer.RenderView(ErrorViewPath, model);
            Application.Response.Write(html);

        }
    }

    public class ErrorModel
    {
        public string Message { get; set; }
        public string FullMessage { get; set; }
        public string StackTrace { get; set; }
        public string Url { get; set; }
        public int StatusCode { get; set; }
        public string StatusDescription { get; set; }

        public override string ToString()
        {
            return string.Format("{0} - {1}", Message, Url);
        }
    }
}