using Meresoft.Umbraco.Banners.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Meresoft.Umbraco.Banners.Delivery;

/// <summary>
/// Puts a small middleware at the very start of the pipeline that removes any <c>Set-Cookie</c> header from the answers of
/// the public endpoints (constitution III: the package sets no cookies). It sits in front of all other middleware, so the
/// header is removed after the last of them has had its say; it is scoped to the public path and touches no other request.
/// </summary>
internal sealed class PublicCookieGuardStartupFilter : IStartupFilter
{
    private readonly PathString _publicPath;

    public PublicCookieGuardStartupFilter(IOptions<BannersOptions> options) => _publicPath = new PathString(options.Value.PublicPath);

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(async (context, nextMiddleware) =>
        {
            if (context.Request.Path.StartsWithSegments(_publicPath, StringComparison.OrdinalIgnoreCase))
            {
                // OnStarting callbacks run last registered first, so this one, registered before every other, runs last.
                context.Response.OnStarting(static state =>
                {
                    ((HttpResponse)state).Headers.Remove("Set-Cookie");
                    return Task.CompletedTask;
                }, context.Response);
            }

            await nextMiddleware(context);
        });
        next(app);
    };
}
