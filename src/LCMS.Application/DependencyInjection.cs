using FluentValidation;
using LCMS.Application.Approvals;
using LCMS.Application.BusinessParties;
using LCMS.Application.Common.Behaviors;
using LCMS.Application.Costs;
using LCMS.Application.Demo;
using LCMS.Application.FinancialCloses;
using LCMS.Application.FinancialControl;
using LCMS.Application.FinancialDocuments;
using LCMS.Application.Fx;
using LCMS.Application.Identity;
using LCMS.Application.OperationalReferences;
using LCMS.Application.RateCards.Commands;
using LCMS.Application.Reconciliations;
using LCMS.Application.ReferenceMasters;
using LCMS.Application.Revenues;
using LCMS.Application.Revenues.Queries;
using LCMS.Application.Settlements;
using LCMS.Application.Tenancy;
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
        services.AddScoped<IPartyDirectoryService, PartyDirectoryService>();
        services.AddScoped<ICanonicalPlaceBinder, CanonicalPlaceBinder>();
        services.AddScoped<IPartySnapshotCapture, PartySnapshotCapture>();
        services.AddScoped<IBillPartyPolicyStore, BillPartyPolicyStore>();
        services.AddScoped<IOperationalCargoStore, OperationalCargoStore>();
        services.AddScoped<OperationalImportBatch>();
        services.AddScoped<RateImportBatch>();
        services.AddScoped<ITenantSettingsService, TenantSettingsService>();
        services.AddScoped<TenantFinancialOptionsResolver>();

        if (configuration is not null)
        {
            services.Configure<CostOptions>(configuration.GetSection(CostOptions.SectionName));
            services.Configure<RevenueOptions>(configuration.GetSection(RevenueOptions.SectionName));
            services.Configure<DocumentOptions>(configuration.GetSection(DocumentOptions.SectionName));
            services.Configure<SettlementOptions>(configuration.GetSection(SettlementOptions.SectionName));
            services.Configure<ApprovalMatrixOptions>(configuration.GetSection(ApprovalMatrixOptions.SectionName));
            services.Configure<FinancialControlOptions>(configuration.GetSection(FinancialControlOptions.SectionName));
            services.Configure<FinancialCloseOptions>(configuration.GetSection(FinancialCloseOptions.SectionName));
            services.Configure<DemoOptions>(configuration.GetSection(DemoOptions.SectionName));
        }
        else
        {
            services.AddOptions<CostOptions>();
            services.AddOptions<RevenueOptions>();
            services.AddOptions<DocumentOptions>();
            services.AddOptions<SettlementOptions>();
            services.AddOptions<ApprovalMatrixOptions>();
            services.AddOptions<FinancialControlOptions>();
            services.AddOptions<FinancialCloseOptions>();
            services.AddOptions<DemoOptions>();
        }

        services.AddScoped<DemoVolumeCatalogSeeder>();
        services.AddScoped<DemoDataSeeder>();

        services.AddScoped<IFxRateLookup, FxRateLookup>();
        services.AddScoped<ICostFxStub, CostFxStub>();
        services.AddScoped<ICostApprovalGate, CostApprovalGate>();
        services.AddScoped<IRevenueFxStub, RevenueFxStub>();
        services.AddScoped<ProfitabilityBoard>();
        services.AddScoped<IRevenueApprovalGate, RevenueApprovalGate>();
        services.AddScoped<ISettlementFxStub, SettlementFxStub>();
        services.AddSingleton<IVarianceSeverityCalculator, VarianceSeverityCalculator>();
        services.AddScoped<ICriticalExceptionConfirmGate, CriticalExceptionConfirmGate>();
        services.AddScoped<ICloseEligibilityChecker, CloseEligibilityChecker>();
        services.AddScoped<IPeriodLockGate, PeriodLockGate>();
        services.AddScoped<IReconciliationDetailWriter, ReconciliationDetailWriter>();
        services.AddScoped<LCMS.Application.Bills.Waybills.WaybillEconomicSeeder>();
        return services;
    }
}
