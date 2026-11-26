using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Meresoft.Umbraco.Banners.Delivery;

/// <summary>
/// Applies the configurable public path to the controllers marked with <see cref="BannersPublicRouteAttribute"/>
/// (research R5), so the endpoints are ordinary attribute routed controllers that win over the content route.
/// </summary>
internal sealed class PublicPathRouteConvention : IApplicationModelConvention
{
    private readonly AttributeRouteModel _prefix;

    public PublicPathRouteConvention(string publicPath) =>
        _prefix = new AttributeRouteModel(new RouteAttribute(publicPath.Trim('/')));

    public void Apply(ApplicationModel application)
    {
        foreach (ControllerModel controller in application.Controllers.Where(c => c.Attributes.OfType<BannersPublicRouteAttribute>().Any()))
        {
            controller.Filters.Add(new PublicResponseHeadersFilter());
            foreach (SelectorModel selector in controller.Actions.SelectMany(a => a.Selectors))
            {
                selector.AttributeRouteModel = selector.AttributeRouteModel is null
                    ? _prefix
                    : AttributeRouteModel.CombineAttributeRouteModel(_prefix, selector.AttributeRouteModel);
            }
        }
    }
}
