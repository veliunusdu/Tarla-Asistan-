using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TarlaAsistani.Application.Common.AI;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.AI.Tools;

namespace TarlaAsistani.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        // Registers all MediatR Handlers (like CreateFarmCommandHandler)
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));

        // Registers all FluentValidation Validators
        services.AddValidatorsFromAssembly(assembly);

        // Fallback unauthenticated context when not provided by host
        services.TryAddScoped<ICurrentUserContext, NullCurrentUserContext>();

        // Time provider and Agent System Prompt Builder
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<TarlaAsistani.Application.Features.AI.Services.IAIAgentSystemPromptBuilder, TarlaAsistani.Application.Features.AI.Services.AIAgentSystemPromptBuilder>();

        // Registers AI Agent Tool Registry and Orchestrator
        services.AddScoped<IAgentToolRegistry, AgentToolRegistry>();
        services.AddScoped<IAIAgentOrchestrator, AIAgentOrchestrator>();
        services.AddOptions<AIAgentOrchestratorOptions>()
            .Configure<IServiceProvider>((options, sp) =>
            {
                var configuration = sp.GetService<Microsoft.Extensions.Configuration.IConfiguration>();
                var maxIterationsStr = configuration?["AI:Agent:MaxIterations"]
                    ?? configuration?["AI_AGENT_MAX_ITERATIONS"]
                    ?? Environment.GetEnvironmentVariable("AI_AGENT_MAX_ITERATIONS");

                if (int.TryParse(maxIterationsStr, out var parsed) && parsed >= 1 && parsed <= 10)
                {
                    options.MaxIterations = parsed;
                }
            });

        // Registers AI Agent Tools
        services.AddScoped<IAgentTool, ListFarmsTool>();
        services.AddScoped<IAgentTool, GetWeatherTool>();
        services.AddScoped<IAgentTool, GetTasksTool>();
        services.AddScoped<IAgentTool, CreateTaskTool>();

        return services;
    }
}