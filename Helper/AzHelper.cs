using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using static VMWAProvision.Helpers.Helper;

namespace VMWAProvision.Helpers
{
    public static class AzureAz
    {
        public static IAzure Az(ILogger log)
        {
            ServicePrincipalLoginInformation principalLogIn = new ServicePrincipalLoginInformation();
            log.LogInformation($"ClientId: {GetEnvironmentVariable("ClientId")}");
            principalLogIn.ClientId = GetEnvironmentVariable("ClientId");
            log.LogInformation($"ClientSecret: {GetEnvironmentVariable("ClientSecret")}");
            principalLogIn.ClientSecret = GetEnvironmentVariable("ClientSecret");

            log.LogInformation($"TenantId: {GetEnvironmentVariable("TenantId")}");
            AzureEnvironment environment = AzureEnvironment.AzureGlobalCloud;
            AzureCredentials credentials = new AzureCredentials(principalLogIn, GetEnvironmentVariable("TenantId"), environment);

            log.LogInformation($"SubscriptionId: {GetEnvironmentVariable("SubscriptionId")}");
            IAzure _azureProd = Microsoft.Azure.Management.Fluent.Azure.Configure()
                .WithLogLevel(HttpLoggingDelegatingHandler.Level.Basic)
                .Authenticate(credentials)
                .WithSubscription(GetEnvironmentVariable("SubscriptionId"));

            log.LogInformation($"Done _azureProd");
            return _azureProd;
        }
    
    }
}
