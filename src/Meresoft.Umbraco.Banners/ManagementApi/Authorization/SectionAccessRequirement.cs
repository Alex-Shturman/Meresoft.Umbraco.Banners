using Microsoft.AspNetCore.Authorization;

namespace Meresoft.Umbraco.Banners.ManagementApi.Authorization;

/// <summary>Requires that the back office user is allowed into the Banners section.</summary>
public sealed class SectionAccessRequirement : IAuthorizationRequirement
{
}
