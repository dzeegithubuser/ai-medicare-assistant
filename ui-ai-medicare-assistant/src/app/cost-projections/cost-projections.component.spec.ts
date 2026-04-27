import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { Router } from '@angular/router';
import { CostProjectionsComponent } from './cost-projections.component';
import { MedicareStateService } from '../services/drug-state.service';
import { AnalysisSnapshotService } from '../services/analysis-snapshot.service';
import { ChartBuilderService } from '../services/chart-builder.service';
import {
  EvaluateCostsResponse,
  IndividualMedicareDetail,
  CostEvaluation,
  LifetimeTotals,
} from '../models/cost-projection.model';
import { AppRoutes } from '../app-routes.const';

/* ── Helpers ──────────────────────────────────────────────── */

function makeYear(overrides: Partial<IndividualMedicareDetail> = {}): IndividualMedicareDetail {
  return {
    year: 2026, monthsUsedForExpenseCalc: 12,
    partAPremium: 0, partBPremium: 170, partBPremiumSurcharge: 0,
    medicareAdvantagePremium: 0, partDPremium: 30, partDPremiumSurcharge: 0,
    conciergePremium: 0, partAOOP: 0, partBOOP: 50, partDOOP: 40,
    totalABMedicareAdvantage: 0, reserveDaysLeft: 60,
    dentalPremium: 0, dentalOOP: 0,
    planGPremium: 100, planFPremium: 0, planNPremium: 0,
    totalABGD: 0, totalABFD: 0, totalABND: 0, totalABCD: 0,
    ...overrides,
  };
}

function makeLifetimeTotals(overrides: Partial<LifetimeTotals> = {}): LifetimeTotals {
  return {
    lifeTimeABMedicareAdvantageExpenses: 500_000, lifeTimeABMedicareAdvantagePremium: 300_000, lifeTimeABMedicareAdvantageOop: 200_000,
    lifeTimeDSurcharge: 0, lifeTimeBSurcharge: 0, totalIrmaa: 5_000,
    lifeTimeConciergePremium: 0, supplementPlanType: 'G', supplementPlanPremium: 100, conciergeIncluded: false,
    lifeTimeABGDExpenses: 600_000, lifeTimeABGDPremium: 400_000, lifeTimeABGDOop: 200_000,
    lifeTimeABFDExpenses: 550_000, lifeTimeABFDPremium: 350_000, lifeTimeABFDOop: 200_000,
    lifeTimeABNDExpenses: 520_000, lifeTimeABNDPremium: 320_000, lifeTimeABNDOop: 200_000,
    lifeTimeABCDExpenses: 510_000, lifeTimeABCDPremium: 310_000, lifeTimeABCDOop: 200_000,
    ...overrides,
  };
}

function makeEvaluation(overrides: Partial<CostEvaluation> = {}): CostEvaluation {
  return {
    planName: 'Test Plan', planBundleCode: 'ABD_G',
    costTrajectory: 'Rising', trajectoryExplanation: 'Costs increase with age',
    overallAssessment: 'Good coverage.',
    lifetimeSummary: { totalPremiums: 400_000, totalOutOfPocket: 200_000, totalCombined: 600_000, projectionYears: 30, averageAnnualCost: 20_000 },
    yearlyHighlights: [{ year: 2026, totalCost: 5000, flag: 'Lowest', explanation: 'First year' }],
    categories: [{ name: 'Part B', lifetimeTotal: 200_000, percentOfTotal: 33, trend: 'Rising', insight: 'Increase' }],
    savingsTips: [{ title: 'Switch plan', description: 'Consider Plan N', estimatedSavings: '$2,000/yr', priority: 'High' }],
    ...overrides,
  };
}

function makeCostProjection(overrides: Partial<EvaluateCostsResponse> = {}): EvaluateCostsResponse {
  return {
    yearlyDetails: [makeYear()],
    lifetimeTotals: makeLifetimeTotals(),
    evaluation: makeEvaluation(),
    presentValue: 450_000,
    ...overrides,
  };
}

/* ── Mock factories ──────────────────────────────────────── */

function createStateMock(hasCost = true) {
  const costProjection = signal<EvaluateCostsResponse | null>(hasCost ? makeCostProjection() : null);
  return {
    costProjection,
    hasCostProjection: signal(hasCost),
    pendingCostRunRecommendationName: signal<string | null>(null),
    setPendingCostRunRecommendationName: vi.fn(),
    resetAll: vi.fn(),
    addAssistantMessage: vi.fn(),
  };
}

describe('CostProjectionsComponent', () => {
  let component: CostProjectionsComponent;
  let fixture: ComponentFixture<CostProjectionsComponent>;
  let stateMock: ReturnType<typeof createStateMock>;
  let routerMock: { navigateByUrl: ReturnType<typeof vi.fn>; events: { pipe: ReturnType<typeof vi.fn> }; url: string };

  function setup(hasCost = true) {
    stateMock = createStateMock(hasCost);
    routerMock = {
      navigateByUrl: vi.fn(),
      events: { pipe: vi.fn().mockReturnValue({ subscribe: vi.fn() }) },
      url: '/cost-projections',
    };

    TestBed.configureTestingModule({
      imports: [CostProjectionsComponent],
      providers: [
        { provide: MedicareStateService, useValue: stateMock },
        { provide: Router, useValue: routerMock },
        { provide: AnalysisSnapshotService, useValue: { canSave: vi.fn().mockReturnValue(false), save: vi.fn() } },
        { provide: ChartBuilderService, useValue: { create: vi.fn(), destroyAll: vi.fn() } },
      ],
    });

    fixture = TestBed.createComponent(CostProjectionsComponent);
    component = fixture.componentInstance;
  }

  afterEach(() => TestBed.resetTestingModule());

  // ─── Lifecycle / Navigation ───────────────────────────────

  it('should create when cost data exists', () => {
    setup(true);
    fixture.detectChanges();
    expect(component).toBeTruthy();
  });

  it('should redirect to profile when no cost data', () => {
    setup(false);
    fixture.detectChanges();
    expect(stateMock.resetAll).toHaveBeenCalled();
    expect(stateMock.addAssistantMessage).toHaveBeenCalled();
    expect(routerMock.navigateByUrl).toHaveBeenCalledWith(AppRoutes.abs.PROFILE, { replaceUrl: true });
  });

  // ─── bundleLabel ──────────────────────────────────────────

  it('should return ABD + G for ABD_G plan', () => {
    setup(true);
    fixture.detectChanges();
    expect(component.bundleLabel).toBe('ABD + G');
  });

  it('should return AB + MA for MA_ONLY plan', () => {
    setup(true);
    stateMock.costProjection.set(makeCostProjection({
      evaluation: makeEvaluation({ planBundleCode: 'MA_ONLY' }),
    }));
    fixture.detectChanges();
    expect(component.bundleLabel).toBe('AB + MA');
  });

  it('should return ABD + MA for MA_PDP plan', () => {
    setup(true);
    stateMock.costProjection.set(makeCostProjection({
      evaluation: makeEvaluation({ planBundleCode: 'MA_PDP' }),
    }));
    fixture.detectChanges();
    expect(component.bundleLabel).toBe('ABD + MA');
  });

  it('should append + Concierge when conciergePremium > 0', () => {
    setup(true);
    stateMock.costProjection.set(makeCostProjection({
      yearlyDetails: [makeYear({ conciergePremium: 200 })],
    }));
    fixture.detectChanges();
    expect(component.bundleLabel).toContain('+ Concierge');
  });

  // ─── expenseTableRow ──────────────────────────────────────

  it('should calculate expense table row for supplement G plan', () => {
    setup(true);
    fixture.detectChanges();
    const row = component.expenseTableRow;
    expect(row).not.toBeNull();
    // partAPremium(0) + partBPremium(170) + partDPremium(30) + planGPremium(100) = 300
    expect(row!.currentTotalPremium).toBe(300);
    // partAOOP(0) + partBOOP(50) + partDOOP(40) = 90
    expect(row!.currentTotalOOP).toBe(90);
    expect(row!.currentTotalExpense).toBe(390);
    // Lifetime from ABG totals
    expect(row!.lifetimeTotalExpense).toBe(600_000);
    expect(row!.lifetimeTotalPremium).toBe(400_000);
    expect(row!.lifetimeTotalOOP).toBe(200_000);
  });

  it('should calculate expense table for MA_ONLY plan', () => {
    setup(true);
    stateMock.costProjection.set(makeCostProjection({
      yearlyDetails: [makeYear({ medicareAdvantagePremium: 50 })],
      evaluation: makeEvaluation({ planBundleCode: 'MA_ONLY' }),
    }));
    fixture.detectChanges();
    const row = component.expenseTableRow;
    expect(row).not.toBeNull();
    // partAPremium(0) + partBPremium(170) + maPremium(50) = 220
    expect(row!.currentTotalPremium).toBe(220);
    // partAOOP(0) + partBOOP(50) = 50
    expect(row!.currentTotalOOP).toBe(50);
    expect(row!.lifetimeTotalExpense).toBe(500_000);
  });

  it('should return null expense table row when no data', () => {
    setup(false);
    expect(component.expenseTableRow).toBeNull();
  });

  // ─── presentValueAmount / coverageYear / totalIrmaaSurcharge ──

  it('should return presentValue from data', () => {
    setup(true);
    fixture.detectChanges();
    expect(component.presentValueAmount).toBe(450_000);
  });

  it('should return coverage year from first yearly detail', () => {
    setup(true);
    fixture.detectChanges();
    expect(component.coverageYear).toBe(2026);
  });

  it('should return totalIrmaaSurcharge from lifetime totals', () => {
    setup(true);
    fixture.detectChanges();
    expect(component.totalIrmaaSurcharge).toBe(5_000);
  });

  // ─── Icon / Color helpers ─────────────────────────────────

  it('should return correct trajectory icons', () => {
    setup(true);
    expect(component.getTrajectoryIcon()).toBe('trending_up');
    stateMock.costProjection.set(makeCostProjection({
      evaluation: makeEvaluation({ costTrajectory: 'Declining' }),
    }));
    expect(component.getTrajectoryIcon()).toBe('trending_down');
  });

  it('should return correct trajectory colors', () => {
    setup(true);
    expect(component.getTrajectoryColor()).toBe('text-red-600');
    stateMock.costProjection.set(makeCostProjection({
      evaluation: makeEvaluation({ costTrajectory: 'Stable' }),
    }));
    expect(component.getTrajectoryColor()).toBe('text-blue-600');
  });

  it('should return correct flag icons', () => {
    setup(true);
    expect(component.getFlagIcon('Highest')).toBe('arrow_upward');
    expect(component.getFlagIcon('Lowest')).toBe('arrow_downward');
    expect(component.getFlagIcon('Spike')).toBe('warning');
    expect(component.getFlagIcon('Normal')).toBe('check_circle');
  });

  it('should return correct flag colors', () => {
    setup(true);
    expect(component.getFlagColor('Highest')).toContain('text-red-600');
    expect(component.getFlagColor('Lowest')).toContain('text-green-600');
    expect(component.getFlagColor('Spike')).toContain('text-amber-600');
  });

  it('should return correct priority colors', () => {
    setup(true);
    expect(component.getPriorityColor('High')).toContain('text-red-700');
    expect(component.getPriorityColor('Medium')).toContain('text-amber-700');
    expect(component.getPriorityColor('Low')).toContain('text-green-700');
    expect(component.getPriorityColor('Unknown')).toContain('text-gray-700');
  });

  // ─── Edge cases ───────────────────────────────────────────

  it('should return empty bundleLabel when no data', () => {
    setup(false);
    expect(component.bundleLabel).toBe('');
  });

  it('should return 0 for coverageYear when no years', () => {
    setup(true);
    stateMock.costProjection.set(makeCostProjection({ yearlyDetails: [] }));
    fixture.detectChanges();
    expect(component.coverageYear).toBe(0);
  });

  it('should add concierge to lifetime expense when concierge premium exists', () => {
    setup(true);
    stateMock.costProjection.set(makeCostProjection({
      lifetimeTotals: makeLifetimeTotals({ lifeTimeConciergePremium: 50_000 }),
    }));
    fixture.detectChanges();
    const row = component.expenseTableRow;
    expect(row).not.toBeNull();
    expect(row!.lifetimeTotalExpense).toBe(650_000);
  });
});
