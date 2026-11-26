using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Strings;
using Meresoft.Umbraco.Banners;

namespace Meresoft.Umbraco.Banners.Sample;

/// <summary>Registers the creation of an invented editor user for permission checks.</summary>
public sealed class SampleUsersComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, SampleEditorCreator>();
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, SampleBannerEditorCreator>();
    }
}

/// <summary>
/// Creates an invented editor in the built-in Editors group (which has no Banners section) when
/// missing. Credentials come from the environment variables MB_SAMPLE_EDITOR_EMAIL and
/// MB_SAMPLE_EDITOR_PASSWORD (or user-secrets with the same names); nothing is created when they
/// are not set. They are never stored in the repository.
/// </summary>
public sealed class SampleEditorCreator : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    private readonly IUserService _userService;
    private readonly IUserGroupService _userGroupService;
    private readonly IBackOfficeUserManager _userManager;
    private readonly IOptions<GlobalSettings> _globalSettings;
    private readonly IRuntimeState _runtimeState;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SampleEditorCreator> _logger;

    public SampleEditorCreator(
        IUserService userService,
        IUserGroupService userGroupService,
        IBackOfficeUserManager userManager,
        IOptions<GlobalSettings> globalSettings,
        IRuntimeState runtimeState,
        IConfiguration configuration,
        ILogger<SampleEditorCreator> logger)
    {
        _userService = userService;
        _userGroupService = userGroupService;
        _userManager = userManager;
        _globalSettings = globalSettings;
        _runtimeState = runtimeState;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task HandleAsync(UmbracoApplicationStartedNotification notification, CancellationToken cancellationToken)
    {
        string? email = _configuration["MB_SAMPLE_EDITOR_EMAIL"];
        string? password = _configuration["MB_SAMPLE_EDITOR_PASSWORD"];
        if (_runtimeState.Level != RuntimeLevel.Run || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        if (_userService.GetByEmail(email) is not null)
        {
            return;
        }

        BackOfficeIdentityUser identity = BackOfficeIdentityUser.CreateNew(_globalSettings.Value, email, email, _globalSettings.Value.DefaultUILanguage);
        identity.Name = "Sample Editor";
        identity.IsApproved = true;
        IdentityResult created = await _userManager.CreateAsync(identity, password);
        if (!created.Succeeded)
        {
            _logger.LogWarning("Sample editor could not be created: {Errors}", string.Join(", ", created.Errors.Select(e => e.Code)));
            return;
        }

        IUser? user = _userService.GetByEmail(email);
        IUserGroup? editors = await _userGroupService.GetAsync(Constants.Security.EditorGroupKey);
        if (user is not null && editors is not null)
        {
            user.AddGroup(editors.ToReadOnlyGroup());
            _userService.Save(user);
        }
    }
}

/// <summary>
/// Creates an invented banner editor: a user in a group that has the Content, Media and Banners sections but not the
/// HTML banner permission. Credentials come from MB_SAMPLE_BANNER_EDITOR_EMAIL and MB_SAMPLE_BANNER_EDITOR_PASSWORD (environment
/// or user-secrets); nothing is created when they are not set.
/// </summary>
public sealed class SampleBannerEditorCreator : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    private const string GroupAlias = "sampleBannerEditors";

    private readonly IUserService _userService;
    private readonly IUserGroupService _userGroupService;
    private readonly IBackOfficeUserManager _userManager;
    private readonly IShortStringHelper _shortStringHelper;
    private readonly IOptions<GlobalSettings> _globalSettings;
    private readonly IRuntimeState _runtimeState;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SampleBannerEditorCreator> _logger;

    public SampleBannerEditorCreator(
        IUserService userService,
        IUserGroupService userGroupService,
        IBackOfficeUserManager userManager,
        IShortStringHelper shortStringHelper,
        IOptions<GlobalSettings> globalSettings,
        IRuntimeState runtimeState,
        IConfiguration configuration,
        ILogger<SampleBannerEditorCreator> logger)
    {
        _userService = userService;
        _userGroupService = userGroupService;
        _userManager = userManager;
        _shortStringHelper = shortStringHelper;
        _globalSettings = globalSettings;
        _runtimeState = runtimeState;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task HandleAsync(UmbracoApplicationStartedNotification notification, CancellationToken cancellationToken)
    {
        if (_runtimeState.Level != RuntimeLevel.Run)
        {
            return;
        }

        // A sample site created before the group had the Media section keeps the old group in its database; bring it up to
        // date on every start. This is the sample's own invented group; the package itself never changes group permissions.
        IUserGroup? group = await _userGroupService.GetAsync(GroupAlias);
        // Picking an image needs the Media section AND a media start node; -1 (Constants.System.Root) is the media root.
        // Only a missing start node is filled in: one someone narrowed to a folder on purpose is left alone.
        if (group is not null && (!group.AllowedSections.Contains(Constants.Applications.Media) || group.StartMediaId is null))
        {
            group.AddAllowedSection(Constants.Applications.Media);
            group.StartMediaId ??= Constants.System.Root;
            var updated = await _userGroupService.UpdateAsync(group, Constants.Security.SuperUserKey);
            if (!updated.Success)
            {
                _logger.LogWarning("Sample banner editor group could not be updated: {Status}", updated.Status);
            }
        }

        string? email = _configuration["MB_SAMPLE_BANNER_EDITOR_EMAIL"];
        string? password = _configuration["MB_SAMPLE_BANNER_EDITOR_PASSWORD"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password) || _userService.GetByEmail(email) is not null)
        {
            return;
        }

        if (group is null)
        {
            var created = new UserGroup(_shortStringHelper)
            {
                Alias = GroupAlias,
                Name = "Sample banner editors",
                HasAccessToAllLanguages = true,
                // Without a media start node the Media section is empty and no image can be picked.
                StartMediaId = Constants.System.Root,
            };
            created.AddAllowedSection(Constants.Applications.Content);
            // Media is needed to pick or upload an image; the HTML permission is deliberately not given.
            created.AddAllowedSection(Constants.Applications.Media);
            created.AddAllowedSection(BannersConstants.SectionAlias);
            var attempt = await _userGroupService.CreateAsync(created, Constants.Security.SuperUserKey, null);
            if (!attempt.Success)
            {
                _logger.LogWarning("Sample banner editor group could not be created: {Status}", attempt.Status);
                return;
            }

            group = attempt.Result;
        }

        BackOfficeIdentityUser identity = BackOfficeIdentityUser.CreateNew(_globalSettings.Value, email, email, _globalSettings.Value.DefaultUILanguage);
        identity.Name = "Sample Banner Editor";
        identity.IsApproved = true;
        IdentityResult result = await _userManager.CreateAsync(identity, password);
        if (!result.Succeeded)
        {
            _logger.LogWarning("Sample banner editor could not be created: {Errors}", string.Join(", ", result.Errors.Select(e => e.Code)));
            return;
        }

        IUser? user = _userService.GetByEmail(email);
        if (user is not null && group is not null)
        {
            user.AddGroup(group.ToReadOnlyGroup());
            _userService.Save(user);
        }
    }
}
