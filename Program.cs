using System;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PnP.Core.Auth.Services.Builder.Configuration;
using PnP.Core.Services.Builder.Configuration;
using FinanceHubFunctions.Services;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // PnP Core SDK setup with Client Secret
        services.AddPnPCore(options =>
        {
            options.Sites.Add("FinanceHub", new PnPCoreSiteOptions
            {
                SiteUrl = Environment.GetEnvironmentVariable("SharePointSiteUrl")
            });
        });

        services.AddPnPCoreAuthentication(options =>
        {
            options.Credentials.Configurations.Add("clientSecret", new PnPCoreAuthenticationCredentialConfigurationOptions
            {
                ClientId = Environment.GetEnvironmentVariable("ClientId"),
                TenantId = Environment.GetEnvironmentVariable("TenantId"),
                ClientSecret = Environment.GetEnvironmentVariable("ClientSecret")
            });

            options.Credentials.DefaultConfiguration = "clientSecret";
            options.Sites.Add("FinanceHub", new PnPCoreAuthenticationSiteOptions { AuthenticationProviderName = "clientSecret" });
        });

        // Register SharePoint service
        services.AddScoped<SharePointService>();
    })
    .Build();

host.Run();
