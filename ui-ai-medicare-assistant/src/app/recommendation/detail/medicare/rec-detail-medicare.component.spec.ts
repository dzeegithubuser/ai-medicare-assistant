import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Component, signal, input } from '@angular/core';
import { RecDetailMedicareComponent } from './rec-detail-medicare.component';
import { ChartBuilderService } from '../../../services/chart-builder.service';
import {
  RecommendationResponse, ProfileSnapshotDto, CostSnapshotDto,
  YearlyDetailDto, CostEvaluationDto,
} from '../../../models/recommendation.model';
import { IndividualMedicareDetail } from '../../../models/cost-projection.model';

/* ── Helpers ──────────────────────────────────────────────── */

function makeProfile(overrides: Partial<ProfileSnapshotDto> = {}): ProfileSnapshotDto {
  return {
    recommendationName: 'Test Rec', firstName: 'John', lastName: 'Doe',
    dateOfBirth: '1960-01-15', gender: 'M', zipCode: '33101', county: 'Miami-Dade',
    countyCode: '12086', state: 'FL', city: 'Miami', addressLine1: '123 Main St',
    healthCondition: 2, lifeExpectancy: 90, tobaccoStatus: 0,
    taxFilingStatus: 'MARRIED_FILING_JOINTLY', magiTier: '1', coverageYear: 2026,
    concierge: 0, conciergeAmount: null, alternateEmail: null, alternateMobile: null,
    latitude: null, longitude: null,
    ...overrides,
  };
}

function makeYearlyDetail(overrides: Partial<YearlyDetailDto> = {}): YearlyDetailDto {
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

function makeEvaluation(overrides: Partial<CostEvaluationDto> = {}): CostEvaluationDto {
  return {
    planName: 'Test Plan', planBundleCode: 'ABD_G',
    costTrajectory: 'Rising', trajectoryExplanation: 'Costs rise',
    overallAssessment: 'Good coverage',
    lifetimeSummary: { totalPremiums: 400_000, totalOutOfPocket: 200_000, totalCombined: 600_000, projectionYears: 30, averageAnnualCost: 20_000 },
    yearlyHighlights: [], categories: [], savingsTips: [],
    ...overrides,
  };
}

function makeCostSnapshot(overrides: Partial<CostSnapshotDto> = {}): CostSnapshotDto {
  return {
    lifetimeTotal: 600_000, lifetimePremiums: 400_000, lifetimeOop: 200_000,
    lifetimeIrmaa: 5_000, presentValue: 450_000, currentYearTotal: 5_000,
    calculatedAt: '2026-01-01', ltcPresentValue: null,
    supplementPlanType: 'G', supplementPlanPremium: 100,
    yearlyDetails: [makeYearlyDetail()],
    evaluation: makeEvaluation(),
    ...overrides,
  };
}

function makeRec(overrides: Partial<RecommendationResponse> = {}): RecommendationResponse {
  return {
    id: 'rec-1', name: 'Test Rec', status: 'Active',
    profile: makeProfile(), planSelections: [], drugList: [],
    pharmacies: [], mailOrderPharmacy: null,
    lastCostSnapshot: makeCostSnapshot(),
    ltcSnapshot: null, createdAt: '2026-01-01', updatedAt: '2026-01-01',
    ...overrides,
  };
}

/**
 * Wrapper host component to supply the required input signal.
 * Angular TestBed cannot set signal inputs directly — use a host.
 */
@Component({
  standalone: true,
  imports: [RecDetailMedicareComponent],
  template: `<app-rec-detail-medicare [rec]="rec()" />`,
})
class TestHostComponent {
  rec = signal(makeRec());
}

describe('RecDetailMedicareComponent', () => {
  let host: TestHostComponent;
  let fixture: ComponentFixture<TestHostComponent>;
  let component: RecDetailMedicareComponent;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [TestHostComponent],
      providers: [
        { provide: ChartBuilderService, useValue: { create: vi.fn(), destroyAll: vi.fn() } },
      ],
    });

    fixture = TestBed.createComponent(TestHostComponent);
    host = fixture.componentInstance;
    fixture.detectChanges();
    component = fixture.debugElement.children[0].componentInstance;
  });

  afterEach(() => TestBed.resetTestingModule());

  // ─── Component creation ───────────────────────────────────

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  // ─── Formatters ───────────────────────────────────────────

  it('should format gender', () => {
    expect(component.fmtGender('M')).toBe('Male');
    expect(component.fmtGender('F')).toBe('Female');
    expect(component.fmtGender('X')).toBe('X');
  });

  it('should format health condition', () => {
    expect(component.fmtHealth(1)).toBe('1 — Best Health');
    expect(component.fmtHealth(3)).toBe('3 — Average Health');
    expect(component.fmtHealth(5)).toBe('5 — Poor Health');
    expect(component.fmtHealth(99)).toBe('99');
  });

  it('should format tax filing status', () => {
    expect(component.fmtTaxFiling('MARRIED_FILING_JOINTLY')).toBe('Married Filing Jointly');
    expect(component.fmtTaxFiling('FILING_INDIVIDUALLY')).toBe('Filing Individually');
    expect(component.fmtTaxFiling('INDIVIDUAL')).toBe('Filing Individually');
    expect(component.fmtTaxFiling('UNKNOWN')).toBe('UNKNOWN');
  });

  it('should format MAGI tier', () => {
    expect(component.fmtMagiTier('1')).toBe('Tier 1');
    expect(component.fmtMagiTier('Custom')).toBe('Custom');
  });

  // ─── URL helpers ──────────────────────────────────────────

  it('should build Google Maps URL', () => {
    const pharmacy = { name: 'CVS', address: '100 Main St', zipCode: '33101' };
    const url = component.getSpotOnMapUrl(pharmacy);
    expect(url).toContain('google.com/maps');
    expect(url).toContain(encodeURIComponent('CVS,100 Main St,33101'));
  });

  it('should build Google Directions URL', () => {
    const pharmacy = { name: 'CVS', address: '100 Main St', zipCode: '33101' };
    const url = component.getDirectionsUrl(pharmacy);
    expect(url).toContain('google.com/maps/dir');
    expect(url).toContain(encodeURIComponent('CVS,100 Main St,33101'));
  });

  // ─── Icon / Color helpers ─────────────────────────────────

  it('should return correct trajectory icons', () => {
    expect(component.getTrajectoryIcon('Rising')).toBe('trending_up');
    expect(component.getTrajectoryIcon('Declining')).toBe('trending_down');
    expect(component.getTrajectoryIcon('Stable')).toBe('trending_flat');
    expect(component.getTrajectoryIcon('Mixed')).toBe('swap_vert');
  });

  it('should return correct trajectory colors', () => {
    expect(component.getTrajectoryColor('Rising')).toBe('text-red-600');
    expect(component.getTrajectoryColor('Declining')).toBe('text-green-600');
    expect(component.getTrajectoryColor('Stable')).toBe('text-blue-600');
  });

  it('should return correct flag icons and colors', () => {
    expect(component.getFlagIcon('Highest')).toBe('arrow_upward');
    expect(component.getFlagIcon('Lowest')).toBe('arrow_downward');
    expect(component.getFlagColor('Highest')).toContain('text-red-600');
    expect(component.getFlagColor('Spike')).toContain('text-amber-600');
  });

  it('should return correct priority colors', () => {
    expect(component.getPriorityColor('High')).toContain('text-red-700');
    expect(component.getPriorityColor('Low')).toContain('text-green-700');
  });

  // ─── bundleLabel ──────────────────────────────────────────

  it('should return ABD + G for supplement G plan', () => {
    expect(component.bundleLabel).toBe('ABD + G');
  });

  it('should return AB + MA for MA_ONLY plan', () => {
    host.rec.set(makeRec({
      lastCostSnapshot: makeCostSnapshot({
        evaluation: makeEvaluation({ planBundleCode: 'MA_ONLY' }),
      }),
    }));
    fixture.detectChanges();
    expect(component.bundleLabel).toBe('AB + MA');
  });

  it('should return ABD + MA for MA_PDP plan', () => {
    host.rec.set(makeRec({
      lastCostSnapshot: makeCostSnapshot({
        evaluation: makeEvaluation({ planBundleCode: 'MA_PDP' }),
      }),
    }));
    fixture.detectChanges();
    expect(component.bundleLabel).toBe('ABD + MA');
  });

  it('should append + Concierge when concierge premium > 0', () => {
    host.rec.set(makeRec({
      lastCostSnapshot: makeCostSnapshot({
        yearlyDetails: [makeYearlyDetail({ conciergePremium: 200 })],
      }),
    }));
    fixture.detectChanges();
    expect(component.bundleLabel).toContain('+ Concierge');
  });

  it('should return empty bundleLabel when no cost snapshot', () => {
    host.rec.set(makeRec({ lastCostSnapshot: null }));
    fixture.detectChanges();
    expect(component.bundleLabel).toBe('');
  });

  // ─── expenseTableRow ──────────────────────────────────────

  it('should calculate expense table row for supplement G plan', () => {
    const row = component.expenseTableRow;
    expect(row).not.toBeNull();
    // partAPremium(0) + partBPremium(170) + partDPremium(30) + planGPremium(100) = 300
    expect(row!.currentTotalPremium).toBe(300);
    // partAOOP(0) + partBOOP(50) + partDOOP(40) = 90
    expect(row!.currentTotalOOP).toBe(90);
    expect(row!.currentTotalExpense).toBe(390);
  });

  it('should calculate MA_ONLY expense table', () => {
    host.rec.set(makeRec({
      lastCostSnapshot: makeCostSnapshot({
        evaluation: makeEvaluation({ planBundleCode: 'MA_ONLY' }),
        yearlyDetails: [makeYearlyDetail({ medicareAdvantagePremium: 50 })],
      }),
    }));
    fixture.detectChanges();
    const row = component.expenseTableRow;
    expect(row).not.toBeNull();
    expect(row!.currentTotalPremium).toBe(220); // 0 + 170 + 50
    expect(row!.currentTotalOOP).toBe(50); // 0 + 50
  });

  it('should return null expense table when no snapshot', () => {
    host.rec.set(makeRec({ lastCostSnapshot: null }));
    fixture.detectChanges();
    expect(component.expenseTableRow).toBeNull();
  });

  // ─── Computed properties ──────────────────────────────────

  it('should return presentValue from snapshot', () => {
    expect(component.presentValue).toBe(450_000);
  });

  it('should return coverageYear from first yearly detail', () => {
    expect(component.coverageYear).toBe(2026);
  });

  it('should return totalIrmaaSurcharge', () => {
    expect(component.totalIrmaaSurcharge).toBe(5_000);
  });

  it('should return 0 for coverageYear when no yearly details', () => {
    host.rec.set(makeRec({
      lastCostSnapshot: makeCostSnapshot({ yearlyDetails: [] }),
    }));
    fixture.detectChanges();
    expect(component.coverageYear).toBe(0);
  });
});
