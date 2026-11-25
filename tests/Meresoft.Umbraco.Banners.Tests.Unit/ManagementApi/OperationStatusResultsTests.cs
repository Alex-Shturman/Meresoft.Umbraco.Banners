using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.ManagementApi;
using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;

namespace Meresoft.Umbraco.Banners.Tests.Unit.ManagementApi;

[TestFixture]
public class OperationStatusResultsTests
{
    private static ObjectResult Map(BannerOperationStatus status, IReadOnlyDictionary<string, string>? errors = null, IReadOnlyList<Guid>? keys = null) =>
        (ObjectResult)OperationStatusResults.ToProblem(status, errors, keys);

    [TestCase(BannerOperationStatus.NotFound, 404)]
    [TestCase(BannerOperationStatus.InvalidData, 400)]
    [TestCase(BannerOperationStatus.NoZoneAssigned, 400)]
    [TestCase(BannerOperationStatus.TypeChangeNotAllowed, 400)]
    [TestCase(BannerOperationStatus.DuplicateAlias, 409)]
    [TestCase(BannerOperationStatus.ZoneInUse, 409)]
    [TestCase(BannerOperationStatus.ChangedByAnotherUser, 409)]
    public void StatusCodes(BannerOperationStatus status, int expected)
    {
        Assert.That(Map(status).StatusCode, Is.EqualTo(expected));
    }

    [TestCase(BannerOperationStatus.ChangedByAnotherUser, "changed-by-another-user")]
    [TestCase(BannerOperationStatus.ZoneInUse, "zone-in-use")]
    [TestCase(BannerOperationStatus.DuplicateAlias, "alias-taken")]
    public void ProblemTypes(BannerOperationStatus status, string type)
    {
        var problem = (ProblemDetails)Map(status).Value!;

        Assert.That(problem.Type, Is.EqualTo(type));
    }

    [Test]
    public void InvalidData_CarriesErrorsPerField()
    {
        var errors = new Dictionary<string, string> { ["alias"] = "bad alias" };

        var problem = (ValidationProblemDetails)Map(BannerOperationStatus.InvalidData, errors).Value!;

        Assert.That(problem.Errors["alias"], Is.EqualTo(new[] { "bad alias" }));
    }

    [Test]
    public void ZoneInUse_ListsTheAssignedBannerKeys()
    {
        Guid key = Guid.NewGuid();

        var problem = (ProblemDetails)Map(BannerOperationStatus.ZoneInUse, keys: [key]).Value!;

        Assert.That(problem.Extensions["bannerKeys"], Is.EqualTo(new[] { key }));
    }

    [Test]
    public void Success_IsNotAProblem()
    {
        Assert.That(() => Map(BannerOperationStatus.Success), Throws.ArgumentException);
    }

    [Test]
    public void HtmlPermissionRequired_Is403WithItsType()
    {
        var result = (ObjectResult)OperationStatusResults.HtmlPermissionRequired();
        var problem = (ProblemDetails)result.Value!;

        Assert.Multiple(() =>
        {
            Assert.That(result.StatusCode, Is.EqualTo(403));
            Assert.That(problem.Type, Is.EqualTo("html-permission-required"));
        });
    }
}
