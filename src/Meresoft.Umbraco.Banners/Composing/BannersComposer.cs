using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace Meresoft.Umbraco.Banners.Composing;

/// <summary>Entry point found by Umbraco when the package is installed.</summary>
public sealed class BannersComposer : IComposer
{
    /// <inheritdoc />
    public void Compose(IUmbracoBuilder builder) => builder.AddMeresoftBanners();
}
