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
using Umbraco.Cms.Core.Security;

namespace Meresoft.Umbraco.Banners.Sample;

/// <summary>Registers the creation of an invented editor user for permission checks.</summary>
public sealed class SampleUsersComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder) =>
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, SampleEditorCreator>();
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
