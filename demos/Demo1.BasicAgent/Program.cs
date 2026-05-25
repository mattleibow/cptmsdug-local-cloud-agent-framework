using System.ClientModel;
using System.ComponentModel;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.DevUI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;

var builder = WebApplication.CreateBuilder(args);

// You will need to set the endpoint to your own value
// You can do this using Visual Studio's "Manage User Secrets" UI, or on the command line:
//   cd this-project-directory
//   dotnet user-secrets set AzureOpenAI:Endpoint https://YOUR-DEPLOYMENT-NAME.openai.azure.com
//   dotnet user-secrets set AzureOpenAI:Key YOUR-API-KEY
var azureOpenAIEndpoint = new Uri(new Uri(builder.Configuration["AzureOpenAI:Endpoint"] ?? throw new InvalidOperationException("Missing configuration: AzureOpenAI:Endpoint")), "/openai/v1");

var chatClient = new ChatClient(
        builder.Configuration["AzureOpenAI:DeploymentName"] ?? "gpt-4o-mini",
        new ApiKeyCredential(builder.Configuration["AzureOpenAI:Key"] ?? throw new InvalidOperationException("Missing configuration: AzureOpenAI:Key")),
        new OpenAIClientOptions { Endpoint = azureOpenAIEndpoint })
    .AsIChatClient();

builder.Services.AddChatClient(chatClient);

// ===== Single Agents =====

builder.AddAIAgent("writer", "You write short stories (300 words or less) about the specified topic.");

builder.AddAIAgent("editor", (sp, key) => new ChatClientAgent(
    chatClient,
    name: key,
    instructions: "You edit short stories to improve grammar and style, ensuring the stories are less than 300 words. Once finished editing, you select a title and format the story for publishing.",
    tools: [AIFunctionFactory.Create(FormatStory)]
));

// ===== Sequential Workflow: Story Pipeline =====

builder.AddWorkflow("publisher", (sp, key) => AgentWorkflowBuilder.BuildSequential(
    workflowName: key,
    agents:
    [
        sp.GetRequiredKeyedService<AIAgent>("writer"),
        sp.GetRequiredKeyedService<AIAgent>("editor")
    ]
)).AddAsAIAgent("publisher");

// ===== Concurrent Agents (Research Briefing) =====

builder.AddAIAgent("technical-analyst",
    "You are a technical analyst. Analyze the technical aspects, feasibility, and implementation details of the given topic. Keep analysis to 150 words.");

builder.AddAIAgent("market-analyst",
    "You are a market analyst. Analyze the market opportunity, competition, and business potential of the given topic. Keep analysis to 150 words.");

builder.AddAIAgent("risk-analyst",
    "You are a risk analyst. Identify potential risks, challenges, and mitigation strategies for the given topic. Keep analysis to 150 words.");

builder.AddAIAgent("synthesizer",
    "You are a synthesis expert. Take multiple analysis reports and combine them into a coherent executive briefing of 200 words or less.");

builder.AddWorkflow("research-briefing", (sp, key) => AgentWorkflowBuilder.BuildSequential(
    workflowName: key,
    agents:
    [
        sp.GetRequiredKeyedService<AIAgent>("technical-analyst"),
        sp.GetRequiredKeyedService<AIAgent>("market-analyst"),
        sp.GetRequiredKeyedService<AIAgent>("risk-analyst"),
        sp.GetRequiredKeyedService<AIAgent>("synthesizer")
    ]
)).AddAsAIAgent("research-briefing");

// ===== Handoff Agents (Customer Support) =====

builder.AddAIAgent("triage", """
    You are a customer support triage agent. Analyze the customer's issue and determine which specialist should handle it.
    Respond with EXACTLY one of these routing decisions:
    - ROUTE:billing - for payment, subscription, or pricing issues
    - ROUTE:technical - for bugs, errors, or technical problems
    - ROUTE:account - for login, password, or account access issues
    After the routing tag, briefly explain why you're routing there.
    """);

builder.AddAIAgent("billing",
    "You are a billing specialist. Help customers with payment issues, subscription changes, refunds, and pricing questions. Be empathetic and solution-oriented. Keep responses under 200 words.");

builder.AddAIAgent("technical",
    "You are a technical support specialist. Help customers debug issues, explain error messages, and provide step-by-step solutions. Be precise and technical. Keep responses under 200 words.");

builder.AddAIAgent("account",
    "You are an account specialist. Help customers with login issues, password resets, account recovery, and access problems. Be patient and clear. Keep responses under 200 words.");

builder.AddWorkflow("customer-support", (sp, key) => AgentWorkflowBuilder.BuildSequential(
    workflowName: key,
    agents:
    [
        sp.GetRequiredKeyedService<AIAgent>("triage"),
        sp.GetRequiredKeyedService<AIAgent>("billing"),
        sp.GetRequiredKeyedService<AIAgent>("technical"),
        sp.GetRequiredKeyedService<AIAgent>("account")
    ]
)).AddAsAIAgent("customer-support");

// ===== Group Chat Agents (Design Review) =====

builder.AddAIAgent("designer",
    "You are a UX designer in a product design review. Evaluate ideas from a usability, aesthetics, and user experience perspective. Challenge engineering constraints when they hurt UX. Keep contributions to 100 words. Address other participants by name.");

builder.AddAIAgent("engineer",
    "You are a software engineer in a product design review. Evaluate ideas from a technical feasibility, performance, and maintainability perspective. Suggest alternatives when designs are too complex. Keep contributions to 100 words. Address other participants by name.");

builder.AddAIAgent("product-manager",
    "You are a product manager in a product design review. Evaluate ideas from a business value, user impact, and timeline perspective. Mediate between design and engineering. Summarize decisions. Keep contributions to 100 words. Address other participants by name.");

builder.AddWorkflow("design-review", (sp, key) => AgentWorkflowBuilder.BuildSequential(
    workflowName: key,
    agents:
    [
        sp.GetRequiredKeyedService<AIAgent>("designer"),
        sp.GetRequiredKeyedService<AIAgent>("engineer"),
        sp.GetRequiredKeyedService<AIAgent>("product-manager")
    ]
)).AddAsAIAgent("design-review");

// Register services for OpenAI responses and conversations (also required for DevUI)
builder.Services.AddOpenAIResponses();
builder.Services.AddOpenAIConversations();

var app = builder.Build();
app.UseHttpsRedirection();

// Map endpoints for OpenAI responses and conversations (also required for DevUI)
app.MapOpenAIResponses();
app.MapOpenAIConversations();

if (builder.Environment.IsDevelopment())
{
    // Map DevUI endpoint to /devui
    app.MapDevUI();
}

app.Run();

[Description("Formats the story for publication, revealing its title.")]
string FormatStory(string title, string story) => $"""
    **Title**: {title}

    {story}
    """;
