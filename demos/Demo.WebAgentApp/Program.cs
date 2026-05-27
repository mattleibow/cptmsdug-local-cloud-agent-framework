using System.ClientModel;
using Demo.Orchestrations;
using Microsoft.Agents.AI.DevUI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;

var builder = WebApplication.CreateBuilder(args);

// Configure Azure OpenAI
var azureOpenAIEndpoint = new Uri(new Uri(builder.Configuration["AzureOpenAI:Endpoint"]
    ?? throw new InvalidOperationException("Missing configuration: AzureOpenAI:Endpoint")), "/openai/v1");

var chatClient = new ChatClient(
        builder.Configuration["AzureOpenAI:DeploymentName"] ?? "gpt-4.1",
        new ApiKeyCredential(builder.Configuration["AzureOpenAI:Key"]
            ?? throw new InvalidOperationException("Missing configuration: AzureOpenAI:Key")),
        new OpenAIClientOptions { Endpoint = azureOpenAIEndpoint })
    .AsIChatClient();

builder.Services.AddChatClient(sp =>
    chatClient.AsBuilder()
        .UseFunctionInvocation()
        .Build(sp));

// Register standalone agents (direct chat)
builder.AddStandaloneAgents();

// Register all workflows (each in its own file)
builder.AddSequentialWorkflow();
builder.AddConcurrentWorkflow();
builder.AddHandoffWorkflow();
builder.AddGroupChatWorkflow();

// Required for DevUI
builder.Services.AddOpenAIResponses();
builder.Services.AddOpenAIConversations();
builder.AddDevUI();

var app = builder.Build();
app.UseHttpsRedirection();
app.MapOpenAIResponses();
app.MapOpenAIConversations();

if (builder.Environment.IsDevelopment())
{
    app.MapDevUI();
}

app.Run();
