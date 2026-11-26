using System.Text.Json.Serialization;

namespace Meresoft.Umbraco.Banners.Delivery.Models;

/// <summary>
/// Answer of <c>GET {PublicPath}/zone/{alias}</c> (contracts/public-endpoints.md). It holds public fields only: never a
/// banner name, weight, schedule, limit, statistic, draft content or user data.
/// </summary>
/// <param name="Zone">The normalised zone alias.</param>
/// <param name="Token">View token for the <c>seen</c> endpoint; left out when no banner is returned.</param>
/// <param name="Banners">The banners to show, in order; empty when the zone is unknown or has no active banner.</param>
public sealed record ZoneDeliveryResponse(
    string Zone,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Token,
    IReadOnlyList<DeliveredBanner> Banners);

/// <summary>A banner in the delivery answer. The <c>type</c> property tells the browser which shape it has.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(DeliveredImageBanner), "image")]
[JsonDerivedType(typeof(DeliveredHtmlBanner), "html")]
public abstract record DeliveredBanner(int Id);

/// <summary>An image banner with its click address (through the click endpoint) and image.</summary>
public sealed record DeliveredImageBanner(
    int Id,
    string ClickUrl,
    bool NewWindow,
    string ImageUrl,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Width,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Height,
    string Alt,
    string? Title) : DeliveredBanner(Id);

/// <summary>An Html banner: the stored markup only. It has no click address (FR-006).</summary>
public sealed record DeliveredHtmlBanner(int Id, string Html) : DeliveredBanner(Id);

/// <summary>Source generated serialisation of the delivery answer, camelCase.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ZoneDeliveryResponse))]
internal sealed partial class ZoneDeliveryJsonContext : JsonSerializerContext;
