using Application.Services;
using Application.Services.Pipeline;
using Domain.Models;
using Microsoft.Extensions.Logging;
using Moq;

namespace AI.MedicareAssistant.Tests;

/// <summary>
/// Tests for DrugAnalysisService — the pipeline orchestrator
/// that runs IDrugAnalysisStep implementations in order.
/// </summary>
public class DrugAnalysisServiceTests
{
    // ═══════ Pipeline Execution ═══════

    [Fact]
    public async Task Analyze_RunsStepsInOrder()
    {
        var callOrder = new List<int>();

        var step1 = CreateMockStep(1, (_, _) => { callOrder.Add(1); return true; });
        var step2 = CreateMockStep(2, (_, _) => { callOrder.Add(2); return true; });
        var step3 = CreateMockStep(3, (_, _) => { callOrder.Add(3); return true; });

        var sut = new DrugAnalysisService(
            [step3, step1, step2], // pass out of order — should sort by Order
            Mock.Of<ILogger<DrugAnalysisService>>());

        await sut.Analyze("test drugs", "80113");

        Assert.Equal([1, 2, 3], callOrder);
    }

    [Fact]
    public async Task Analyze_ShortCircuitsOnFalseReturn()
    {
        var callOrder = new List<int>();

        var step1 = CreateMockStep(1, (_, _) => { callOrder.Add(1); return true; });
        var step2 = CreateMockStep(2, (_, _) => { callOrder.Add(2); return false; }); // short-circuit
        var step3 = CreateMockStep(3, (_, _) => { callOrder.Add(3); return true; });

        var sut = new DrugAnalysisService(
            [step1, step2, step3],
            Mock.Of<ILogger<DrugAnalysisService>>());

        await sut.Analyze("test drugs");

        Assert.Equal([1, 2], callOrder);
        Assert.DoesNotContain(3, callOrder);
    }

    [Fact]
    public async Task Analyze_PassesPrescriptionAndZipInContext()
    {
        string? capturedPrescription = null;
        string? capturedZip = null;

        var step = CreateMockStep(1, (_, ctx) =>
        {
            capturedPrescription = ctx.Prescription;
            capturedZip = ctx.ZipCode;
            return true;
        });

        var sut = new DrugAnalysisService([step], Mock.Of<ILogger<DrugAnalysisService>>());
        await sut.Analyze("Eliquis 5mg", "90210");

        Assert.Equal("Eliquis 5mg", capturedPrescription);
        Assert.Equal("90210", capturedZip);
    }

    [Fact]
    public async Task Analyze_NoSteps_ReturnsEmptyResult()
    {
        var sut = new DrugAnalysisService([], Mock.Of<ILogger<DrugAnalysisService>>());

        var result = await sut.Analyze("test");

        Assert.Empty(result.Drugs);
        Assert.Empty(result.Interactions);
    }

    [Fact]
    public async Task Analyze_StepMutatesResult_VisibleToNextStep()
    {
        var step1 = CreateMockStep(1, (result, _) =>
        {
            result.Drugs.Add(new DrugResult { NormalizedDrugName = "metformin", GenericName = "metformin" });
            return true;
        });

        DrugAnalysisResult? capturedResult = null;
        var step2 = CreateMockStep(2, (result, _) =>
        {
            capturedResult = result;
            return true;
        });

        var sut = new DrugAnalysisService([step1, step2], Mock.Of<ILogger<DrugAnalysisService>>());
        await sut.Analyze("metformin 500mg");

        Assert.NotNull(capturedResult);
        Assert.Single(capturedResult!.Drugs);
        Assert.Equal("metformin", capturedResult.Drugs[0].NormalizedDrugName);
    }

    [Fact]
    public async Task Analyze_NullZipCode_DefaultsCorrectly()
    {
        string? capturedZip = null;
        var step = CreateMockStep(1, (_, ctx) => { capturedZip = ctx.ZipCode; return true; });

        var sut = new DrugAnalysisService([step], Mock.Of<ILogger<DrugAnalysisService>>());
        await sut.Analyze("test");

        Assert.Null(capturedZip);
    }

    // ═══════ Helpers ═══════

    private static IDrugAnalysisStep CreateMockStep(int order, Func<DrugAnalysisResult, AnalysisContext, bool> execute)
    {
        var mock = new Mock<IDrugAnalysisStep>();
        mock.Setup(s => s.Order).Returns(order);
        mock.Setup(s => s.ExecuteAsync(It.IsAny<DrugAnalysisResult>(), It.IsAny<AnalysisContext>()))
            .ReturnsAsync((DrugAnalysisResult r, AnalysisContext c) => execute(r, c));
        return mock.Object;
    }
}
