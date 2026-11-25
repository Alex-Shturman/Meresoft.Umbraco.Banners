using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Meresoft.Umbraco.Banners.Delivery;

/// <summary>
/// Headers of every public endpoint response (contracts/public-endpoints.md): never cached, never indexed, and never a cookie.
/// </summary>
internal sealed class PublicResponseHeadersFilter : IAlwaysRunResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        HttpResponse response = context.HttpContext.Response;
        response.Headers.CacheControl = "no-store";
        response.Headers["X-Robots-Tag"] = "noindex, nofollow";

        // These endpoints never set a cookie; this keeps it that way if another component of the site tries to.
        response.OnStarting(static state =>
        {
            ((HttpResponse)state).Headers.Remove("Set-Cookie");
            return Task.CompletedTask;
        }, response);
    }

    public void OnResultExecuted(ResultExecutedContext context)
    {
    }
}
