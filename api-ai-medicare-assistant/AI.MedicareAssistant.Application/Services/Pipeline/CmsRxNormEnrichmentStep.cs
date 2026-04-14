using Domain.Interfaces;
using Domain.Models;
using Microsoft.Extensions.Logging;

namespace Application.Services.Pipeline;

/// <summary>
/// Step 3: Enriches each drug with CMS Medicare cost estimates,
/// RxNorm normalization data, and package-accurate NDC codes via FDA NDC Directory.
/// </summary>
public class CmsRxNormEnrichmentStep : IDrugAnalysisStep
{
    private readonly IMedicareCostService _medicare;
    private readonly IRxNormService _rxNorm;
    private readonly IFdaNdcService _fdaNdc;
    private readonly ILogger<CmsRxNormEnrichmentStep> _logger;

    public int Order => 3;

    public CmsRxNormEnrichmentStep(
        IMedicareCostService medicare,
        IRxNormService rxNorm,
        IFdaNdcService fdaNdc,
        ILogger<CmsRxNormEnrichmentStep> logger)
    {
        _medicare = medicare;
        _rxNorm = rxNorm;
        _fdaNdc = fdaNdc;
        _logger = logger;
    }

    public async Task<bool> ExecuteAsync(DrugAnalysisResult result, AnalysisContext context)
    {
        var enrichmentTasks = result.Drugs.Select(async drug =>
        {
            var lookupName = !string.IsNullOrWhiteSpace(drug.GenericName)
                ? drug.GenericName
                : drug.NormalizedDrugName;

            var cmsTask = _medicare.GetCostEstimate(lookupName);
            var rxNormTask = _rxNorm.NormalizeDrug(lookupName);

            await Task.WhenAll(cmsTask, rxNormTask);

            drug.MedicareCostEstimate = await cmsTask;

            var rxResult = await rxNormTask;
            if (rxResult != null && string.IsNullOrWhiteSpace(drug.RxNormId))
            {
                drug.RxNormId = rxResult.RxCui;
            }

            // Resolve NDC codes: RxNorm → FDA NDC → package-accurate matching
            if (!string.IsNullOrWhiteSpace(drug.RxNormId))
            {
                var ndcs = await _rxNorm.GetNdcsByRxCui(drug.RxNormId);
                if (ndcs.Count > 0)
                {
                    // Fetch FDA package info for all NDCs
                    var packageInfos = await _fdaNdc.GetPackageInfoBatch(ndcs);

                    if (packageInfos.Count > 0 && drug.Formulations.Count > 0)
                    {
                        // Package-accurate matching: match FDA package descriptions to AI formulation packaging
                        MatchNdcsToFormulations(drug, packageInfos);
                    }
                    else
                    {
                        // Fallback: assign NDCs by index position
                        for (int i = 0; i < drug.Formulations.Count; i++)
                        {
                            if (i < ndcs.Count)
                                drug.Formulations[i].NdcCode = ndcs[i];
                        }
                    }

                    // Fill flat array for backward compat
                    drug.NdcCodes = ndcs;

                    _logger.LogInformation("Enriched {Drug} with {Count} NDC code(s) (FDA matched: {FdaCount})",
                        drug.NormalizedDrugName, ndcs.Count, packageInfos.Count);
                }
            }
        });

        await Task.WhenAll(enrichmentTasks);

        _logger.LogInformation("CMS + RxNorm + FDA enrichment complete for {Count} drug(s)", result.Drugs.Count);
        return true;
    }

    /// <summary>
    /// Matches FDA package descriptions to AI-generated formulation packaging strings.
    /// E.g., FDA "60 TABLET in 1 BOTTLE" matches formulation packaging "Bottle of 60 tablets".
    /// </summary>
    private void MatchNdcsToFormulations(DrugResult drug, List<NdcPackageInfo> packageInfos)
    {
        var unmatched = new List<NdcPackageInfo>(packageInfos);

        foreach (var formulation in drug.Formulations)
        {
            NdcPackageInfo? bestMatch = null;
            int bestScore = -1;

            foreach (var pkg in unmatched)
            {
                var score = CalculateMatchScore(formulation.Packaging, pkg);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestMatch = pkg;
                }
            }

            if (bestMatch != null && bestScore > 0)
            {
                formulation.NdcCode = bestMatch.NdcCode;
                unmatched.Remove(bestMatch);
                _logger.LogDebug("Matched formulation '{Packaging}' → NDC {Ndc} (FDA: '{Desc}', score: {Score})",
                    formulation.Packaging, bestMatch.NdcCode, bestMatch.PackageDescription, bestScore);
            }
        }

        // For any unmatched formulations, assign remaining NDCs by position
        var unmatchedFormulations = drug.Formulations.Where(f => string.IsNullOrWhiteSpace(f.NdcCode)).ToList();
        for (int i = 0; i < unmatchedFormulations.Count && i < unmatched.Count; i++)
        {
            unmatchedFormulations[i].NdcCode = unmatched[i].NdcCode;
        }
    }

    /// <summary>
    /// Scores how well an FDA package description matches a formulation packaging string.
    /// Higher score = better match. Returns 0 if no meaningful match.
    /// </summary>
    private static int CalculateMatchScore(string formPackaging, NdcPackageInfo fdaPackage)
    {
        if (string.IsNullOrWhiteSpace(formPackaging) || string.IsNullOrWhiteSpace(fdaPackage.PackageDescription))
            return 0;

        int score = 0;
        var packaging = formPackaging.ToLowerInvariant();
        var fdaDesc = fdaPackage.PackageDescription.ToLowerInvariant();
        var fdaType = fdaPackage.PackageType.ToLowerInvariant();

        // Match package size: "Bottle of 60 tablets" vs PackageSize=60
        if (fdaPackage.PackageSize > 0 && packaging.Contains(fdaPackage.PackageSize.ToString()))
            score += 10;

        // Match container type
        if (!string.IsNullOrWhiteSpace(fdaType))
        {
            if (fdaType.Contains("bottle") && packaging.Contains("bottle"))
                score += 5;
            else if (fdaType.Contains("blister") && packaging.Contains("blister"))
                score += 5;
            else if (fdaType.Contains("tube") && packaging.Contains("tube"))
                score += 5;
            else if (fdaType.Contains("vial") && packaging.Contains("vial"))
                score += 5;
            else if (fdaType.Contains("syringe") && packaging.Contains("syringe"))
                score += 5;
            else if (fdaType.Contains("box") && packaging.Contains("box"))
                score += 5;
            else if (fdaType.Contains("canister") && packaging.Contains("canister"))
                score += 5;
            else if (fdaType.Contains("inhaler") && packaging.Contains("inhaler"))
                score += 5;
        }

        // Match dosage form hints in FDA description
        if (fdaDesc.Contains("tablet") && packaging.Contains("tablet"))
            score += 3;
        else if (fdaDesc.Contains("capsule") && packaging.Contains("capsule"))
            score += 3;
        else if (fdaDesc.Contains("ml") && packaging.Contains("ml"))
            score += 3;

        return score;
    }
}
