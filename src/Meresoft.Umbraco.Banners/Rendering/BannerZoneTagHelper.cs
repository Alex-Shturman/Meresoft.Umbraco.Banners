using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Meresoft.Umbraco.Banners.Rendering;

/// <summary>
/// <c>&lt;banner-zone alias="search-top" class="my-ads"&gt;fallback&lt;/banner-zone&gt;</c>: places a
/// banner zone (contracts/markup.md). The child content is the fallback.
/// </summary>
[HtmlTargetElement("banner-zone")]
public sealed class BannerZoneTagHelper : TagHelper
{
    private readonly BannerZoneRenderer _renderer;

    /// <summary>Creates the tag helper.</summary>
    public BannerZoneTagHelper(BannerZoneRenderer renderer) => _renderer = renderer;

    /// <summary>Zone alias; normalised before use.</summary>
    [HtmlAttributeName("alias")]
    public string? Alias { get; set; }

    /// <summary>Extra CSS classes for the zone element.</summary>
    [HtmlAttributeName("class")]
    public string? CssClass { get; set; }

    /// <summary>The current view context.</summary>
    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = null!;

    /// <inheritdoc />
    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        ZoneSelection selection = await _renderer.SelectAsync(Alias, ViewContext.HttpContext);

        // The child content is the fallback: it is only run (it may be expensive Razor) when no banner is shown.
        string? fallback = null;
        if (selection.Banners.Count == 0)
        {
            TagHelperContent child = await output.GetChildContentAsync();
            fallback = child.GetContent();
        }

        string html = _renderer.Render(ViewContext.HttpContext.Items!, selection, CssClass, fallback);

        output.TagName = null;
        output.Content.SetHtmlContent(html);
    }
}
