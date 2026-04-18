using Domain.Interfaces;
using Infrastructure.CountyLookup;
using Infrastructure.Fda;
using Infrastructure.Medicare;

namespace Api.Extensions;

internal static class HttpClientExtensions
{
    internal static IServiceCollection AddInfrastructureHttpClients(this IServiceCollection services)
    {
        // ------- CMS Medicare -------
        services.AddHttpClient<IMedicareCostService, CmsMedicareCostService>(c =>
            c.Timeout = TimeSpan.FromSeconds(10));

        // ------- FDA NDC Directory -------
        services.AddHttpClient<IFdaNdcService, FdaNdcService>(c =>
            c.Timeout = TimeSpan.FromSeconds(10));

        // ------- Pharmacy Lookup (Financial Planner) -------
        services.AddHttpClient<IPharmacyLookupService, Infrastructure.Pharmacy.FinancialPlannerPharmacyService>(c =>
            c.Timeout = TimeSpan.FromSeconds(15));

        // ------- County Code Lookup -------
        services.AddHttpClient<ICountyLookupService, CountyLookupService>(c =>
            c.Timeout = TimeSpan.FromSeconds(10));

        // ------- CMS Plan Data -------
        services.AddHttpClient<ICmsPlanDataService, CmsPlanDataService>(c =>
            c.Timeout = TimeSpan.FromSeconds(10));

        // ------- Financial Planner: Constants -------
        services.AddHttpClient<IConstantsService, Infrastructure.FinancialPlanner.FinancialPlannerConstantsService>(c =>
            c.Timeout = TimeSpan.FromSeconds(10));

        // ------- Financial Planner: Individual Medicare -------
        services.AddHttpClient<IIndividualMedicareService, Infrastructure.FinancialPlanner.IndividualMedicareService>(c =>
            c.Timeout = TimeSpan.FromSeconds(30));

        // ------- Financial Planner: Drug Search -------
        services.AddHttpClient<IFinancialPlannerDrugService, Infrastructure.FinancialPlanner.FinancialPlannerDrugService>(c =>
            c.Timeout = TimeSpan.FromSeconds(15));

        // ------- Financial Planner: Part D Plan Recommendation -------
        services.AddHttpClient<IPartDPlanRecommendationService, Infrastructure.FinancialPlanner.PartDPlanRecommendationService>(c =>
            c.Timeout = TimeSpan.FromSeconds(30));

        // ------- Financial Planner: Medigap Plan Quotes -------
        services.AddHttpClient<IMedigapPlanQuotesService, Infrastructure.FinancialPlanner.MedigapPlanQuotesService>(c =>
            c.Timeout = TimeSpan.FromSeconds(30));

        // ------- Financial Planner: Medicare Advantage -------
        services.AddHttpClient<IMedicareAdvantagePlanService, Infrastructure.FinancialPlanner.MedicareAdvantagePlanService>(c =>
            c.Timeout = TimeSpan.FromSeconds(30));

        // ------- Financial Planner: Long Term Care -------
        services.AddHttpClient<ILongTermCareService, Infrastructure.FinancialPlanner.LongTermCareService>(c =>
            c.Timeout = TimeSpan.FromSeconds(30));

        // ------- Financial Planner: Present Value -------
        services.AddHttpClient<IPresentValueService, Infrastructure.FinancialPlanner.PresentValueService>(c =>
            c.Timeout = TimeSpan.FromSeconds(30));

        return services;
    }
}
