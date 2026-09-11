using FluentValidation;
using LCMS.Application.Common.Behaviors;
using LCMS.Application.Identity;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace LCMS.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddScoped<IOrganizationHierarchyService, OrganizationHierarchyService>();
        return services;
    }
}
