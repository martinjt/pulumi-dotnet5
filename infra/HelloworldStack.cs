using Pulumi;
using Pulumi.AzureNative.Resources;
using static FunctionApp;

class HelloworldStack : Stack
{
    public HelloworldStack()
    {
        var resourceGroup = new ResourceGroup("main", new ResourceGroupArgs{
            Location = "uksouth"
        });

        var function = new FunctionApp("helloworld", new FunctionAppArgs {
            Location = "uksouth",
            ResourceGroupName = resourceGroup.Name,
        });

        this.Url = Output.Format($"https://{function.DefaultHostname}/api/hello");
    }

    [Output]
    public Output<string> Url { get; set; }
}
