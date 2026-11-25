using Meresoft.Umbraco.Banners.ManagementApi.Banners.Models;
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
        options.SchemaFilter<BannersWeightSchemaFilter>();
    }

    /// <summary>
    /// Writes the 1 to 100 range of a banner's weight into the schema. It is done here and not with a validation
    /// attribute, because an attribute makes the framework validate the body itself and answer with its own error
    /// shape; the package validator is the only source of the weight error.
    /// </summary>
    public sealed class BannersWeightSchemaFilter : ISchemaFilter
    {
        /// <inheritdoc />
        public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
        {
            if (context.Type != typeof(BannerRequestModel) || schema.Properties is null)
            {
                return;
            }

            if (schema.Properties.TryGetValue("weight", out IOpenApiSchema? weight) && weight is OpenApiSchema writable)
            {
                writable.Minimum = "1";
                writable.Maximum = "100";
            }
        }
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
