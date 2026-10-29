using System;
using System.Net;

namespace Contoso.Purchasing.Web.Services
{
    public class ApiException : Exception
    {
        public ApiException(HttpStatusCode status, string message) : base(message)
        {
            Status = status;
        }

        public HttpStatusCode Status { get; private set; }
    }
}
