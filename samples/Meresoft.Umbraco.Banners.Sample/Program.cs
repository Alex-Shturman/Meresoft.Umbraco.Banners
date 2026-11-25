WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddComposers()
    .Build();

// Output cache for the page /cached (60 s), used to check banners on cached pages.
builder.Services.AddOutputCache(options =>
    options.AddBasePolicy(policy => policy
        .With(context => context.HttpContext.Request.Path.StartsWithSegments("/cached"))
        .Expire(TimeSpan.FromSeconds(60))));

WebApplication app = builder.Build();

await app.BootUmbracoAsync();

app.UseOutputCache();

app.UseUmbraco()
    .WithMiddleware(u =>
    {
        u.UseBackOffice();
        u.UseWebsite();
    })
    .WithEndpoints(u =>
    {
        u.UseBackOfficeEndpoints();
        u.UseWebsiteEndpoints();
    });

await app.RunAsync();
