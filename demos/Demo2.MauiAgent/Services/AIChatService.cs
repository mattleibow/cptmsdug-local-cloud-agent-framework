using System.ClientModel;
using System.Reflection;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;
using OpenAI.Chat;

namespace Demo2.MauiAgent.Services;

public class AIChatService
{
    private readonly IChatClient _chatClient;
    private readonly string _deploymentName;

    public AIChatService(IConfiguration configuration)
    {
        var endpoint = configuration["AI:Endpoint"]
            ?? throw new InvalidOperationException("Missing configuration: AI:Endpoint");
        var apiKey = configuration["AI:ApiKey"]
            ?? throw new InvalidOperationException("Missing configuration: AI:ApiKey");
        _deploymentName = configuration["AI:DeploymentName"] ?? "gpt-4.1";

        var azureOpenAIEndpoint = new Uri(new Uri(endpoint), "/openai/v1");

        _chatClient = new ChatClient(
                _deploymentName,
                new ApiKeyCredential(apiKey),
                new OpenAIClientOptions { Endpoint = azureOpenAIEndpoint })
            .AsIChatClient()
            .AsBuilder()
            .UseFunctionInvocation()
            .Build();
    }

    public IChatClient ChatClient => _chatClient;
    public string DeploymentName => _deploymentName;

    public static void AddUserSecrets(ConfigurationManager manager)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceNames = assembly.GetManifestResourceNames();
        var secretsResource = resourceNames.FirstOrDefault(n => n.EndsWith("secrets.json"));
        if (secretsResource is not null)
        {
            using var stream = assembly.GetManifestResourceStream(secretsResource);
            if (stream is not null)
                manager.AddJsonStream(stream);
        }
    }
}
