using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

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

        return services;
    }
}