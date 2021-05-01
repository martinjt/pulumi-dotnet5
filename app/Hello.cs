using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace app
{
    public static class Hello
    {
        [Function("Hello")]
        public static HttpResponseData Run([HttpTrigger(AuthorizationLevel.Anonymous, "GET", Route = "hello")] HttpRequestData req,
            FunctionContext executionContext)
        {
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");

            response.WriteString("Welcome to Azure Functions using .NET 5 and Pulumi!");

            return response;
        }
    }
}
