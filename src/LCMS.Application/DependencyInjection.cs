using FluentValidation;
using LCMS.Application.Common.Behaviors;
using LCMS.Application.Costs;
using LCMS.Application.FinancialDocuments;
using LCMS.Application.Identity;
using LCMS.Application.Revenues;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LCMS.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration? configuration = null)
    {
        var assembly = typeof(DependencyInjection).Assembly;
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddScoped<IOrganizationHierarchyService, OrganizationHierarchyService>();

        if (configuration is not null)
        {
            services.Configure<CostOptions>(configuration.GetSection(CostOptions.SectionName));
            services.Configure<RevenueOptions>(configuration.GetSection(RevenueOptions.SectionName));
            services.Configure<DocumentOptions>(configuration.GetSection(DocumentOptions.SectionName));
        }
        else
        {
            services.AddOptions<CostOptions>();
            services.AddOptions<RevenueOptions>();
            services.AddOptions<DocumentOptions>();
        }

        services.AddSingleton<ICostFxStub, CostFxStub>();
        services.AddSingleton<ICostApprovalGate, CostApprovalGate>();
        services.AddSingleton<IRevenueFxStub, RevenueFxStub>();
        services.AddSingleton<IRevenueApprovalGate, RevenueApprovalGate>();
        return services;
    }
}
