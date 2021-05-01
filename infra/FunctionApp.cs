using System;
using Pulumi;
using Pulumi.AzureNative.Insights;
using Pulumi.AzureNative.Storage;
using Pulumi.AzureNative.Storage.Inputs;
using Pulumi.AzureNative.Web;
using Pulumi.AzureNative.Web.Inputs;

/// <summary>
/// Self Contained function app with it's own appservice plan
/// </summary>
public class FunctionApp : ComponentResource
{
    public Output<string> AppId { get; private set; } = null!;
    public Output<string> DefaultHostname { get; private set; } = null!;
    public Output<string> AppName { get; set; }

    public FunctionApp(string name, FunctionAppArgs args, ComponentResourceOptions? options = null)
        : base("infra:functionapp", name, options)
    {
        var opts = new CustomResourceOptions { Parent = this };

        var appStorage = new StorageAccount(name.Replace("-", ""), new StorageAccountArgs
        {
            ResourceGroupName = args.ResourceGroupName,
            Sku = new SkuArgs
            {
                Name = SkuName.Standard_LRS,
            },
            Kind = Pulumi.AzureNative.Storage.Kind.StorageV2,
        });

        var appServicePlan = new AppServicePlan(name, new AppServicePlanArgs
        {
            ResourceGroupName = args.ResourceGroupName,
            Location = args.Location,
            Kind = "FunctionApp",
            Sku = new SkuDescriptionArgs
            {
                Tier = "Dynamic",
                Name = "Y1"
            }
        }, opts);

        var container = new BlobContainer("code-container", new BlobContainerArgs
        {
            AccountName = appStorage.Name,
            PublicAccess = PublicAccess.None,
            ResourceGroupName = args.ResourceGroupName,
        });

        var blob = new Blob($"zip-{DateTime.UtcNow:ddMMyyyyhhmmss}", new BlobArgs
        {
            AccountName = appStorage.Name,
            ContainerName = container.Name,
            ResourceGroupName = args.ResourceGroupName,
            Type = BlobType.Block,
            Source = new FileArchive("../publish")
        });

        var appInsights = new Component("appInsights", new ComponentArgs
        {
            ApplicationType = ApplicationType.Web,
            Kind = "web",
            ResourceGroupName = args.ResourceGroupName,
        });

        var codeBlobUrl = SignedBlobReadUrl(blob, container, appStorage, args.ResourceGroupName);

        var accountKeys = Output.Tuple(args.ResourceGroupName, appStorage.Name)
            .Apply(p => ListStorageAccountKeys.InvokeAsync(new ListStorageAccountKeysArgs
            {
                ResourceGroupName = p.Item1,
                AccountName = p.Item2
            }));

        var storageConnectionString =
            Output.Format(
                $"DefaultEndpointsProtocol=https;AccountName={appStorage.Name};AccountKey={accountKeys.Apply(a => a.Keys[0].Value)}");

        var app = new WebApp(name, new WebAppArgs
        {
            Kind = "FunctionApp",
            ResourceGroupName = args.ResourceGroupName,
            ServerFarmId = appServicePlan.Id,
            SiteConfig = new SiteConfigArgs
            {
                AppSettings = new[]
                {
                        new NameValuePairArgs
                        {
                            Name = "APPLICATIONINSIGHTS_CONNECTION_STRING",
                            Value = Output.Format($"InstrumentationKey={appInsights.InstrumentationKey}"),
                        },
                        new NameValuePairArgs
                        {
                            Name = "FUNCTIONS_EXTENSION_VERSION",
                            Value = "~3"
                        },
                        new NameValuePairArgs
                        {
                            Name = "FUNCTIONS_WORKER_RUNTIME",
                            Value = "dotnet-isolated"
                        },
                        new NameValuePairArgs{
                            Name = "WEBSITE_RUN_FROM_PACKAGE",
                            Value = codeBlobUrl,
                        },
                        new NameValuePairArgs
                        {
                            Name = "AzureWebJobsStorage",
                            Value = GetConnectionString(args.ResourceGroupName, appStorage.Name)
                        }
                    }
            }
        });

        this.AppName = app.Name;
        this.AppId = app.Id;
        this.DefaultHostname = app.DefaultHostName;
    }

    private static Output<string> SignedBlobReadUrl(Blob blob, BlobContainer container, StorageAccount account, Output<string> resourceGroupName)
    {
        return Output.Tuple<string, string, string, string>(
            blob.Name, container.Name, account.Name, resourceGroupName).Apply(t =>
        {
            (string blobName, string containerName, string accountName, string resourceGroupName) = t;

            var blobSAS = ListStorageAccountServiceSAS.InvokeAsync(new ListStorageAccountServiceSASArgs
            {
                AccountName = accountName,
                Protocols = HttpProtocol.Https,
                SharedAccessStartTime = "2021-01-01",
                SharedAccessExpiryTime = "2030-01-01",
                Resource = SignedResource.C,
                ResourceGroupName = resourceGroupName,
                Permissions = Permissions.R,
                CanonicalizedResource = "/blob/" + accountName + "/" + containerName,
                ContentType = "application/json",
                CacheControl = "max-age=5",
                ContentDisposition = "inline",
                ContentEncoding = "deflate",
            });
            return Output.Format($"https://{accountName}.blob.core.windows.net/{containerName}/{blobName}?{blobSAS.Result.ServiceSasToken}");
        });
    }

    private static Output<string> GetConnectionString(Input<string> resourceGroupName, Input<string> accountName)
    {
        // Retrieve the primary storage account key.
        var storageAccountKeys = Output.All<string>(resourceGroupName, accountName).Apply(t =>
        {
            var resourceGroupName = t[0];
            var accountName = t[1];
            return ListStorageAccountKeys.InvokeAsync(
                new ListStorageAccountKeysArgs
                {
                    ResourceGroupName = resourceGroupName,
                    AccountName = accountName
                });
        });
        return storageAccountKeys.Apply(keys =>
        {
            var primaryStorageKey = keys.Keys[0].Value;

                // Build the connection string to the storage account.
                return Output.Format($"DefaultEndpointsProtocol=https;AccountName={accountName};AccountKey={primaryStorageKey}");
        });
    }

    public class FunctionAppArgs
    {
        public Output<string> ResourceGroupName { get; set; } = null!;
        public string Location { get; set; } = null!;
    }
}