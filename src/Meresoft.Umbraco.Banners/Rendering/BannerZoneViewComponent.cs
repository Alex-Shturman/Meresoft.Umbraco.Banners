using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewComponents;

namespace Meresoft.Umbraco.Banners.Rendering;

/// <summary><c>@await Component.InvokeAsync("BannerZone", new { alias = "search-top" })</c>: same as the tag helper, without fallback content.</summary>
[ViewComponent(Name = "BannerZone")]
public sealed class BannerZoneViewComponent : ViewComponent
{
    private readonly BannerZoneRenderer _renderer;
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>Creates the view component.</summary>
    public BannerZoneViewComponent(BannerZoneRenderer renderer, IHttpContextAccessor httpContextAccessor)
    {
        _renderer = renderer;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>Renders the zone.</summary>
    public async Task<IViewComponentResult> InvokeAsync(string alias, string? cssClass = null)
    {
        HttpContext? httpContext = _httpContextAccessor.HttpContext;
        IDictionary<object, object?> items = httpContext?.Items ?? new Dictionary<object, object?>();
        ZoneSelection selection = await _renderer.SelectAsync(alias, httpContext);
        return new HtmlContentViewComponentResult(new HtmlString(_renderer.Render(items, selection, cssClass, null, httpContext?.Request.PathBase ?? default)));
    }
}
