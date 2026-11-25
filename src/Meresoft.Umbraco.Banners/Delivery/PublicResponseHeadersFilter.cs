using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Meresoft.Umbraco.Banners.Delivery;

/// <summary>
/// Headers of every public endpoint response (contracts/public-endpoints.md): never cached and never indexed.
/// </summary>
internal sealed class PublicResponseHeadersFilter : IAlwaysRunResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        HttpResponse response = context.HttpContext.Response;
        response.Headers.CacheControl = "no-store";
        response.Headers["X-Robots-Tag"] = "noindex, nofollow";
        // No cookie handling here: a result filter runs too late to beat other middleware.
        // PublicCookieGuardStartupFilter removes any Set-Cookie header at the start of the pipeline.
    }

    public void OnResultExecuted(ResultExecutedContext context)
    {
    }
}
