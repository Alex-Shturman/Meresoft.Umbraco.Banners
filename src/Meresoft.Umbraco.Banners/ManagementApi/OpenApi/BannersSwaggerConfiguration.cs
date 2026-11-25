using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using Umbraco.Cms.Api.Management.OpenApi;

namespace Meresoft.Umbraco.Banners.ManagementApi.OpenApi;

/// <summary>
/// Registers the OpenAPI document of the package. This is the only file that uses Swashbuckle
/// types; Umbraco 18 moves to Microsoft.AspNetCore.OpenApi, so the change stays in one place (research R4).
/// </summary>
public sealed class BannersSwaggerConfiguration : IConfigureOptions<SwaggerGenOptions>
{
    /// <inheritdoc />
    public void Configure(SwaggerGenOptions options)
    {
        options.SwaggerDoc(
            BannersConstants.ApiName,
            new OpenApiInfo
            {
                Title = "Meresoft Banners Management API",
                Version = "Latest",
                Description = "Back office API of the Meresoft Banners package for Umbraco.",
            });

        options.OperationFilter<BannersBackOfficeSecurityFilter>();
        options.OperationFilter<BannersOperationIdFilter>();
    }

    /// <summary>Declares the back office bearer security of the package operations.</summary>
    public sealed class BannersBackOfficeSecurityFilter : BackOfficeSecurityRequirementsOperationFilterBase
    {
        /// <inheritdoc />
        protected override string ApiName => BannersConstants.ApiName;
    }

    /// <summary>
    /// Gives each package operation a short id taken from its controller name (<c>ListZonesController</c> becomes
    /// <c>listZones</c>), so the generated back office client has readable function names. Other documents are not touched.
    /// </summary>
    public sealed class BannersOperationIdFilter : IOperationFilter
    {
        /// <inheritdoc />
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            if (context.ApiDescription.ActionDescriptor is ControllerActionDescriptor action
                && context.ApiDescription.GroupName == BannersConstants.ApiGroupName
                && action.ControllerTypeInfo.Assembly == typeof(BannersConstants).Assembly)
            {
                string name = action.ControllerName;
                operation.OperationId = char.ToLowerInvariant(name[0]) + name[1..];
            }
        }
    }
}
