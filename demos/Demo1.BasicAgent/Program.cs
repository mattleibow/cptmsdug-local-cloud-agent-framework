using System.ClientModel;
using System.ComponentModel;
using Demo.Orchestrations;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.DevUI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;

var builder = WebApplication.CreateBuilder(args);

// Configure Azure OpenAI
//   dotnet user-secrets set AzureOpenAI:Endpoint https://YOUR-DEPLOYMENT-NAME.openai.azure.com
//   dotnet user-secrets set AzureOpenAI:Key YOUR-API-KEY
//   dotnet user-secrets set AzureOpenAI:DeploymentName gpt-4.1
var azureOpenAIEndpoint = new Uri(new Uri(builder.Configuration["AzureOpenAI:Endpoint"]
    ?? throw new InvalidOperationException("Missing configuration: AzureOpenAI:Endpoint")), "/openai/v1");

var chatClient = new ChatClient(
        builder.Configuration["AzureOpenAI:DeploymentName"] ?? "gpt-4.1",
        new ApiKeyCredential(builder.Configuration["AzureOpenAI:Key"]
            ?? throw new InvalidOperationException("Missing configuration: AzureOpenAI:Key")),
        new OpenAIClientOptions { Endpoint = azureOpenAIEndpoint })
    .AsIChatClient();

builder.Services.AddChatClient(chatClient);

// ===== Register all agents from shared orchestrations =====

// Standalone agents
foreach (var agent in DemoWorkflows.StandaloneAgents)
{
    if (agent.Name == "editor")
    {
        // Editor has the FormatStory tool
        builder.AddAIAgent(agent.Name, (sp, key) => new ChatClientAgent(
            chatClient,
            name: key,
            instructions: agent.SystemPrompt,
            tools: [AIFunctionFactory.Create(FormatStory)]));
    }
    else
    {
        builder.AddAIAgent(agent.Name, agent.SystemPrompt);
    }
}

// Sequential Workflow: Story Pipeline
var sequential = DemoWorkflows.Sequential;
builder.AddWorkflow("publisher", (sp, key) => AgentWorkflowBuilder.BuildSequential(
    workflowName: key,
    agents:
    [
        sp.GetRequiredKeyedService<AIAgent>("writer"),
        sp.GetRequiredKeyedService<AIAgent>("editor")
    ]
)).AddAsAIAgent("publisher");

// Concurrent: Research Briefing agents
foreach (var agent in DemoWorkflows.Concurrent.Agents)
{
    builder.AddAIAgent(agent.Name, agent.SystemPrompt);
}

builder.AddWorkflow("research-briefing", (sp, key) => AgentWorkflowBuilder.BuildSequential(
    workflowName: key,
    agents: DemoWorkflows.Concurrent.Agents
        .Select(a => sp.GetRequiredKeyedService<AIAgent>(a.Name))
        .ToArray()
)).AddAsAIAgent("research-briefing");

// Handoff: Customer Support agents
foreach (var agent in DemoWorkflows.Handoff.Agents)
{
    builder.AddAIAgent(agent.Name, agent.SystemPrompt);
}

// Group Chat: Design Review agents
foreach (var agent in DemoWorkflows.GroupChat.Agents)
{
    builder.AddAIAgent(agent.Name, agent.SystemPrompt);
}

// Register services for OpenAI responses and conversations (required for DevUI)
builder.Services.AddOpenAIResponses();
builder.Services.AddOpenAIConversations();

var app = builder.Build();
app.UseHttpsRedirection();

// Map endpoints for OpenAI responses and conversations (required for DevUI)
app.MapOpenAIResponses();
app.MapOpenAIConversations();

if (builder.Environment.IsDevelopment())
{
    app.MapDevUI();
}

app.Run();

[Description("Formats the story for publication, revealing its title.")]
string FormatStory(string title, string story) => $"""
    **Title**: {title}

    {story}
    """;
