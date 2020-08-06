using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleApp3.Handler
{
    public class HttpsGuardHandler : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (!request.RequestUri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.CurrentCultureIgnoreCase))
            {
                var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent("Https is required for security reason")
                };
                return Task.FromResult(response);
            }
            try
            {
                var res = base.SendAsync(request, cancellationToken);
                return res;
            }
            catch (System.Exception ex)
            {
                return null;
            }
        }
    }
}
