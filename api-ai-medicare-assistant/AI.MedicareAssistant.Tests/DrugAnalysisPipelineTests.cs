using System.Text.Json;
using Application.Services.Pipeline;
using Domain.Interfaces;
using Domain.Models;
using Microsoft.Extensions.Logging;
using Moq;

namespace AI.MedicareAssistant.Tests;

/// <summary>
/// Tests for the 4-step drug analysis pipeline:
/// AiAnalysisStep, DrugValidationStep, CmsRxNormEnrichmentStep, InteractionMergingStep.
/// </summary>
public class DrugAnalysisPipelineTests
{
    // ═══════════════════════════════════════════════════════════════
    //  AiAnalysisStep
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task AiAnalysisStep_ValidJson_PopulatesResult()
    {
        var aiMock = new Mock<IDrugAiService>();
        var aiResponse = JsonSerializer.Serialize(new DrugAnalysisResult
        {
            Drugs = [CreateDrugResult("apixaban", "Eliquis")],
            Interactions = [],
            DosageAlerts = [],
            DuplicateTherapies = [],
            Message = "Analysis complete"
        });
        aiMock.Setup(a => a.AnalyzePrescription("Eliquis 5mg")).ReturnsAsync(aiResponse);

        var step = new AiAnalysisStep(aiMock.Object, Mock.Of<ILogger<AiAnalysisStep>>());
        var result = new DrugAnalysisResult();
        var context = new AnalysisContext("Eliquis 5mg", "80113");

        var shouldContinue = await step.ExecuteAsync(result, context);

        Assert.True(shouldContinue);
        Assert.Single(result.Drugs);
        Assert.Equal("apixaban", result.Drugs[0].NormalizedDrugName);
        Assert.Equal("Analysis complete", result.Message);
    }

    [Fact]
    public async Task AiAnalysisStep_NullParsedResult_ReturnsTrueContinues()
    {
        var aiMock = new Mock<IDrugAiService>();
        aiMock.Setup(a => a.AnalyzePrescription(It.IsAny<string>())).ReturnsAsync("null");

        var step = new AiAnalysisStep(aiMock.Object, Mock.Of<ILogger<AiAnalysisStep>>());
        var result = new DrugAnalysisResult();
        var context = new AnalysisContext("test", null);

        var shouldContinue = await step.ExecuteAsync(result, context);

        Assert.True(shouldContinue);
        Assert.Empty(result.Drugs);
    }

    [Fact]
    public async Task AiAnalysisStep_MalformedJson_ThrowsJsonException()
    {
        var aiMock = new Mock<IDrugAiService>();
        aiMock.Setup(a => a.AnalyzePrescription(It.IsAny<string>())).ReturnsAsync("not valid json {{{");

        var step = new AiAnalysisStep(aiMock.Object, Mock.Of<ILogger<AiAnalysisStep>>());
        var result = new DrugAnalysisResult();
        var context = new AnalysisContext("test", null);

        await Assert.ThrowsAsync<JsonException>(() => step.ExecuteAsync(result, context));
    }

    [Fact]
    public async Task AiAnalysisStep_AiServiceThrows_PropagatesException()
    {
        var aiMock = new Mock<IDrugAiService>();
        aiMock.Setup(a => a.AnalyzePrescription(It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("AI service down"));

        var step = new AiAnalysisStep(aiMock.Object, Mock.Of<ILogger<AiAnalysisStep>>());
        var result = new DrugAnalysisResult();
        var context = new AnalysisContext("test", null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => step.ExecuteAsync(result, context));
    }

    // ═══════════════════════════════════════════════════════════════
    //  DrugValidationStep
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task DrugValidationStep_ValidDrugs_ReturnsTrue()
    {
        var step = new DrugValidationStep(Mock.Of<ILogger<DrugValidationStep>>());
        var result = new DrugAnalysisResult
        {
            Drugs = [CreateDrugWithFormulations("apixaban", "Eliquis")]
        };
        var context = new AnalysisContext("Eliquis 5mg", "80113");

        var shouldContinue = await step.ExecuteAsync(result, context);

        Assert.True(shouldContinue);
        Assert.Single(result.Drugs);
        Assert.Equal("80113", result.ZipCode);
    }

    [Fact]
    public async Task DrugValidationStep_NoDrugs_ShortCircuits()
    {
        var step = new DrugValidationStep(Mock.Of<ILogger<DrugValidationStep>>());
        var result = new DrugAnalysisResult { Drugs = [] };
        var context = new AnalysisContext("garbage", null);

        var shouldContinue = await step.ExecuteAsync(result, context);

        Assert.False(shouldContinue);
        Assert.Contains("No valid drugs", result.Message);
    }

    [Fact]
    public async Task DrugValidationStep_FiltersMissingNormalizedName()
    {
        var step = new DrugValidationStep(Mock.Of<ILogger<DrugValidationStep>>());
        var result = new DrugAnalysisResult
        {
            Drugs =
            [
                CreateDrugWithFormulations("apixaban", "Eliquis"),
                CreateDrugResult("", "Unknown") // invalid — no normalized name
            ]
        };
        var context = new AnalysisContext("test", null);

        await step.ExecuteAsync(result, context);

        Assert.Single(result.Drugs);
        Assert.Equal("apixaban", result.Drugs[0].NormalizedDrugName);
    }

    [Fact]
    public async Task DrugValidationStep_PopulatesFlatArraysFromFormulations()
    {
        var step = new DrugValidationStep(Mock.Of<ILogger<DrugValidationStep>>());
        var drug = CreateDrugWithFormulations("apixaban", "Eliquis");
        var result = new DrugAnalysisResult { Drugs = [drug] };
        var context = new AnalysisContext("test", null);

        await step.ExecuteAsync(result, context);

        Assert.Contains("tablet", result.Drugs[0].DosageForms);
        Assert.Contains("5 mg", result.Drugs[0].Strengths);
        Assert.Contains("Bottle of 60 tablets", result.Drugs[0].Packaging);
    }

    [Fact]
    public async Task DrugValidationStep_AllInvalid_ShortCircuitsWithMessage()
    {
        var step = new DrugValidationStep(Mock.Of<ILogger<DrugValidationStep>>());
        var result = new DrugAnalysisResult
        {
            Drugs =
            [
                new DrugResult { NormalizedDrugName = "", GenericName = "", DosageForms = [] }
            ]
        };
        var context = new AnalysisContext("xyz", null);

        var shouldContinue = await step.ExecuteAsync(result, context);

        Assert.False(shouldContinue);
        Assert.Empty(result.Drugs);
        Assert.Contains("No valid drugs", result.Message);
    }

    // ═══════════════════════════════════════════════════════════════
    //  CmsRxNormEnrichmentStep
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task CmsRxNormEnrichmentStep_EnrichesDrugWithCmsAndRxNorm()
    {
        var medicareMock = new Mock<IMedicareCostService>();
        medicareMock.Setup(m => m.GetCostEstimate("apixaban"))
            .ReturnsAsync(new MedicareCostEstimate { DrugName = "ELIQUIS", AverageCostPerClaim = 285.50m });

        var rxNormMock = new Mock<IRxNormService>();
        rxNormMock.Setup(r => r.NormalizeDrug("apixaban"))
            .ReturnsAsync(new RxNormResult { RxCui = "1364430" });
        rxNormMock.Setup(r => r.GetNdcsByRxCui("1364430"))
            .ReturnsAsync(new List<string> { "00003-0894-21" });

        var fdaNdcMock = new Mock<IFdaNdcService>();
        fdaNdcMock.Setup(f => f.GetPackageInfoBatch(It.IsAny<List<string>>()))
            .ReturnsAsync(new List<NdcPackageInfo>());

        var step = new CmsRxNormEnrichmentStep(
            medicareMock.Object, rxNormMock.Object, fdaNdcMock.Object,
            Mock.Of<ILogger<CmsRxNormEnrichmentStep>>());

        var drug = CreateDrugWithFormulations("apixaban", "Eliquis");
        drug.RxNormId = null; // should be filled by enrichment
        var result = new DrugAnalysisResult { Drugs = [drug] };
        var context = new AnalysisContext("Eliquis 5mg", null);

        var shouldContinue = await step.ExecuteAsync(result, context);

        Assert.True(shouldContinue);
        Assert.NotNull(result.Drugs[0].MedicareCostEstimate);
        Assert.Equal("1364430", result.Drugs[0].RxNormId);
        Assert.Contains("00003-0894-21", result.Drugs[0].NdcCodes);
    }

    [Fact]
    public async Task CmsRxNormEnrichmentStep_NullRxNormResult_SkipsNdcEnrichment()
    {
        var medicareMock = new Mock<IMedicareCostService>();
        medicareMock.Setup(m => m.GetCostEstimate(It.IsAny<string>())).ReturnsAsync((MedicareCostEstimate?)null);

        var rxNormMock = new Mock<IRxNormService>();
        rxNormMock.Setup(r => r.NormalizeDrug(It.IsAny<string>())).ReturnsAsync((RxNormResult?)null);

        var fdaNdcMock = new Mock<IFdaNdcService>();

        var step = new CmsRxNormEnrichmentStep(
            medicareMock.Object, rxNormMock.Object, fdaNdcMock.Object,
            Mock.Of<ILogger<CmsRxNormEnrichmentStep>>());

        var drug = CreateDrugWithFormulations("unknowndrug", "Unknown");
        drug.RxNormId = null;
        var result = new DrugAnalysisResult { Drugs = [drug] };
        var context = new AnalysisContext("test", null);

        await step.ExecuteAsync(result, context);

        Assert.Null(result.Drugs[0].RxNormId);
        Assert.Empty(result.Drugs[0].NdcCodes);
        fdaNdcMock.Verify(f => f.GetPackageInfoBatch(It.IsAny<List<string>>()), Times.Never);
    }

    [Fact]
    public async Task CmsRxNormEnrichmentStep_MultipleDrugs_EnrichesAll()
    {
        var medicareMock = new Mock<IMedicareCostService>();
        medicareMock.Setup(m => m.GetCostEstimate(It.IsAny<string>())).ReturnsAsync((MedicareCostEstimate?)null);

        var rxNormMock = new Mock<IRxNormService>();
        rxNormMock.Setup(r => r.NormalizeDrug(It.IsAny<string>()))
            .ReturnsAsync(new RxNormResult { RxCui = "12345" });
        rxNormMock.Setup(r => r.GetNdcsByRxCui(It.IsAny<string>()))
            .ReturnsAsync(new List<string>());

        var fdaNdcMock = new Mock<IFdaNdcService>();

        var step = new CmsRxNormEnrichmentStep(
            medicareMock.Object, rxNormMock.Object, fdaNdcMock.Object,
            Mock.Of<ILogger<CmsRxNormEnrichmentStep>>());

        var result = new DrugAnalysisResult
        {
            Drugs =
            [
                CreateDrugWithFormulations("metformin", "Metformin"),
                CreateDrugWithFormulations("lisinopril", "Lisinopril")
            ]
        };
        var context = new AnalysisContext("test", null);

        await step.ExecuteAsync(result, context);

        Assert.All(result.Drugs, d => Assert.Equal("12345", d.RxNormId));
    }

    // ═══════════════════════════════════════════════════════════════
    //  InteractionMergingStep
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task InteractionMergingStep_MergesNewInteractions()
    {
        var rxNormMock = new Mock<IRxNormService>();
        rxNormMock.Setup(r => r.GetInteractions(It.IsAny<List<string>>()))
            .ReturnsAsync(new List<DrugInteraction>
            {
                new() { DrugA = "Warfarin", DrugB = "Aspirin", Severity = "High", Description = "Bleeding risk" }
            });

        var step = new InteractionMergingStep(rxNormMock.Object, Mock.Of<ILogger<InteractionMergingStep>>());
        var result = new DrugAnalysisResult
        {
            Drugs =
            [
                new DrugResult { RxNormId = "11289", NormalizedDrugName = "warfarin", GenericName = "warfarin" },
                new DrugResult { RxNormId = "1191", NormalizedDrugName = "aspirin", GenericName = "aspirin" }
            ],
            Interactions = [] // AI found none
        };
        var context = new AnalysisContext("test", null);

        await step.ExecuteAsync(result, context);

        Assert.Single(result.Interactions);
        Assert.Equal("Warfarin", result.Interactions[0].DrugA);
    }

    [Fact]
    public async Task InteractionMergingStep_DeduplicatesExistingInteractions()
    {
        var rxNormMock = new Mock<IRxNormService>();
        rxNormMock.Setup(r => r.GetInteractions(It.IsAny<List<string>>()))
            .ReturnsAsync(new List<DrugInteraction>
            {
                new() { DrugA = "Warfarin", DrugB = "Aspirin", Severity = "High" }
            });

        var step = new InteractionMergingStep(rxNormMock.Object, Mock.Of<ILogger<InteractionMergingStep>>());
        var result = new DrugAnalysisResult
        {
            Drugs =
            [
                new DrugResult { RxNormId = "11289", NormalizedDrugName = "warfarin", GenericName = "warfarin" },
                new DrugResult { RxNormId = "1191", NormalizedDrugName = "aspirin", GenericName = "aspirin" }
            ],
            Interactions = new List<DrugInteraction>
            {
                new() { DrugA = "WARFARIN", DrugB = "ASPIRIN", Severity = "High" } // AI already found this
            }
        };
        var context = new AnalysisContext("test", null);

        await step.ExecuteAsync(result, context);

        Assert.Single(result.Interactions); // not duplicated
    }

    [Fact]
    public async Task InteractionMergingStep_SingleDrug_SkipsInteractionCheck()
    {
        var rxNormMock = new Mock<IRxNormService>();
        var step = new InteractionMergingStep(rxNormMock.Object, Mock.Of<ILogger<InteractionMergingStep>>());
        var result = new DrugAnalysisResult
        {
            Drugs = [new DrugResult { RxNormId = "11289", NormalizedDrugName = "warfarin", GenericName = "warfarin" }],
            Interactions = []
        };
        var context = new AnalysisContext("test", null);

        await step.ExecuteAsync(result, context);

        rxNormMock.Verify(r => r.GetInteractions(It.IsAny<List<string>>()), Times.Never);
    }

    [Fact]
    public async Task InteractionMergingStep_NoRxCuis_SkipsInteractionCheck()
    {
        var rxNormMock = new Mock<IRxNormService>();
        var step = new InteractionMergingStep(rxNormMock.Object, Mock.Of<ILogger<InteractionMergingStep>>());
        var result = new DrugAnalysisResult
        {
            Drugs =
            [
                new DrugResult { RxNormId = null, NormalizedDrugName = "drug1", GenericName = "drug1" },
                new DrugResult { RxNormId = "", NormalizedDrugName = "drug2", GenericName = "drug2" }
            ],
            Interactions = []
        };
        var context = new AnalysisContext("test", null);

        await step.ExecuteAsync(result, context);

        rxNormMock.Verify(r => r.GetInteractions(It.IsAny<List<string>>()), Times.Never);
    }

    // ═══════ Helpers ═══════

    private static DrugResult CreateDrugResult(string normalizedName, string brandName) => new()
    {
        NormalizedDrugName = normalizedName,
        GenericName = normalizedName,
        BrandNames = [brandName],
        DosageForms = ["tablet"],
        Strengths = ["5 mg"],
        Packaging = ["Bottle of 60 tablets"],
        Formulations = []
    };

    private static DrugResult CreateDrugWithFormulations(string normalizedName, string brandName) => new()
    {
        NormalizedDrugName = normalizedName,
        GenericName = normalizedName,
        BrandNames = [brandName],
        DosageForms = [],
        Strengths = [],
        Packaging = [],
        NdcCodes = [],
        Formulations =
        [
            new DrugFormulation
            {
                DosageForm = "tablet",
                Strength = "5 mg",
                Packaging = "Bottle of 60 tablets",
                NdcCode = null
            }
        ]
    };
}
