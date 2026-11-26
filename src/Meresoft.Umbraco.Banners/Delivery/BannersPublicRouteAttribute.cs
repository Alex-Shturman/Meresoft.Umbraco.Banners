namespace Meresoft.Umbraco.Banners.Delivery;

/// <summary>
/// Marks a controller of the public endpoints. <see cref="PublicPathRouteConvention"/> puts the configured
/// <c>Meresoft:Banners:PublicPath</c> in front of its routes and adds the response headers of the public endpoints.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class BannersPublicRouteAttribute : Attribute
{
}
