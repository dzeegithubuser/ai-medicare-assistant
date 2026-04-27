import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { Router } from '@angular/router';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { of, EMPTY } from 'rxjs';
import { UserProfileComponent } from './user-profile.component';
import { ProfileService } from '../services/profile.service';
import { CountyLookupService, CountyCodeEntry } from '../services/county-lookup.service';
import { ReferenceDataService } from '../services/reference-data.service';
import { MedicareStateService } from '../services/drug-state.service';
import { ChatWizardService } from '../services/chat-wizard.service';
import { ChatProfileEditFlowService } from '../services/chat-profile-edit-flow.service';
import { MatSnackBar } from '@angular/material/snack-bar';

/* ── Mock factories ──────────────────────────────────────── */

function createProfileServiceMock() {
  return {
    profile: signal(null),
    isProfileComplete: signal(false),
    profileLoadSettled: signal(true),
    pendingPrefill: signal<Record<string, unknown> | null>(null),
    pendingChatProfileData: signal(null),
    missingRequiredFields: signal<string[]>([]),
    chatSaveRequestId: signal(0),
    chatSaveInProgress: signal(false),
    chatDiscardRequestId: signal(0),
    saveProfile: vi.fn().mockReturnValue(of({ success: true })),
    load: vi.fn(),
  };
}

function createCountyServiceMock() {
  return {
    getCountyCodeList: vi.fn().mockReturnValue(EMPTY),
    getMagiTiers: vi.fn().mockReturnValue(of([
      { label: 'Tier 1 — $0–$206,000', value: 1 },
      { label: 'Tier 2 — $206,001–$258,000', value: 2 },
    ])),
  };
}

function createRefDataMock() {
  return {
    load: vi.fn(),
    usStates: signal([{ label: 'Florida', value: 'FL' }]),
    taxFilingStatuses: signal([
      { label: 'Married Filing Jointly', value: 'MARRIED_FILING_JOINTLY' },
      { label: 'Filing Individually', value: 'FILING_INDIVIDUALLY' },
    ]),
    filingStatuses: signal([]),
    healthConditions: signal([]),
  };
}

function createDrugStateMock() {
  return {
    currentStep: signal(1),
    addAssistantMessage: vi.fn(),
    addSystemMessage: vi.fn(),
    setLoading: vi.fn(),
    returnRoute: signal(null),
    invalidateAfterProfileChange: vi.fn(),
    resetAll: vi.fn(),
  };
}

describe('UserProfileComponent', () => {
  let component: UserProfileComponent;
  let fixture: ComponentFixture<UserProfileComponent>;
  let profileMock: ReturnType<typeof createProfileServiceMock>;
  let countyMock: ReturnType<typeof createCountyServiceMock>;
  let routerMock: { url: string; navigate: ReturnType<typeof vi.fn>; navigateByUrl: ReturnType<typeof vi.fn> };

  beforeEach(() => {
    profileMock = createProfileServiceMock();
    countyMock = createCountyServiceMock();
    routerMock = { url: '/profile', navigate: vi.fn(), navigateByUrl: vi.fn() };

    TestBed.configureTestingModule({
      imports: [UserProfileComponent, NoopAnimationsModule],
      providers: [
        { provide: ProfileService, useValue: profileMock },
        { provide: CountyLookupService, useValue: countyMock },
        { provide: ReferenceDataService, useValue: createRefDataMock() },
        { provide: MedicareStateService, useValue: createDrugStateMock() },
        { provide: ChatWizardService, useValue: { medicareProfileIntroComplete: signal(false), ltcProfileIntroComplete: signal(false) } },
        { provide: ChatProfileEditFlowService, useValue: { hasUnsavedProfileChanges: signal(false) } },
        { provide: Router, useValue: routerMock },
        { provide: MatSnackBar, useValue: { open: vi.fn() } },
      ],
    });

    fixture = TestBed.createComponent(UserProfileComponent);
    component = fixture.componentInstance;
  });

  afterEach(() => TestBed.resetTestingModule());

  // ─── Component Creation ───────────────────────────────────

  it('should create', () => {
    fixture.detectChanges();
    expect(component).toBeTruthy();
  });

  // ─── Form Initialization ──────────────────────────────────

  it('should create form with all required controls', () => {
    fixture.detectChanges();
    const controls = component.form.controls;
    expect(controls.firstName).toBeDefined();
    expect(controls.lastName).toBeDefined();
    expect(controls.dateOfBirth).toBeDefined();
    expect(controls.gender).toBeDefined();
    expect(controls.healthCondition).toBeDefined();
    expect(controls.taxFilingStatus).toBeDefined();
    expect(controls.magiTier).toBeDefined();
    expect(controls.zipCode).toBeDefined();
    expect(controls.county).toBeDefined();
    expect(controls.countyCode).toBeDefined();
    expect(controls.lifeExpectancy).toBeDefined();
    expect(controls.concierge).toBeDefined();
    expect(controls.addressLine1).toBeDefined();
  });

  it('should have default values', () => {
    fixture.detectChanges();
    expect(component.form.controls.gender.value).toBe('F');
    expect(component.form.controls.healthCondition.value).toBe(1);
    expect(component.form.controls.taxFilingStatus.value).toBe('MARRIED_FILING_JOINTLY');
    expect(component.form.controls.lifeExpectancy.value).toBe(95);
    expect(component.form.controls.concierge.value).toBe(0);
  });

  // ─── Form Validation ─────────────────────────────────────

  it('should require firstName', () => {
    fixture.detectChanges();
    expect(component.form.controls.firstName.valid).toBe(false);
    component.form.controls.firstName.setValue('John');
    expect(component.form.controls.firstName.valid).toBe(true);
  });

  it('should reject invalid name patterns', () => {
    fixture.detectChanges();
    component.form.controls.firstName.setValue('John123');
    expect(component.form.controls.firstName.valid).toBe(false);
    component.form.controls.firstName.setValue("O'Brien");
    expect(component.form.controls.firstName.valid).toBe(true);
    component.form.controls.firstName.setValue('Smith-Jones');
    expect(component.form.controls.firstName.valid).toBe(true);
  });

  it('should validate ZIP code format', () => {
    fixture.detectChanges();
    const zip = component.form.controls.zipCode;
    zip.setValue('abc');
    expect(zip.valid).toBe(false);
    zip.setValue('33101');
    expect(zip.valid).toBe(true);
    zip.setValue('33101-1234');
    expect(zip.valid).toBe(true);
  });

  it('should validate lifeExpectancy range', () => {
    fixture.detectChanges();
    const le = component.form.controls.lifeExpectancy;
    le.setValue(50);
    expect(le.valid).toBe(false);
    le.setValue(95);
    expect(le.valid).toBe(true);
    le.setValue(130);
    expect(le.valid).toBe(false);
  });

  it('should validate alternate email format', () => {
    fixture.detectChanges();
    const email = component.form.controls.alternateEmail;
    email.setValue('invalid');
    expect(email.valid).toBe(false);
    email.setValue('test@example.com');
    expect(email.valid).toBe(true);
    email.setValue('');
    expect(email.valid).toBe(true); // optional field
  });

  // ─── Date of Birth / Medicare Age ─────────────────────────

  it('should reject underage date of birth', () => {
    fixture.detectChanges();
    const dob = component.form.controls.dateOfBirth;
    const recentDate = new Date();
    recentDate.setFullYear(recentDate.getFullYear() - 10);
    dob.setValue(recentDate);
    expect(dob.hasError('minAge')).toBe(true);
  });

  it('should accept valid date of birth', () => {
    fixture.detectChanges();
    const dob = component.form.controls.dateOfBirth;
    dob.setValue(new Date('1960-01-15T00:00:00'));
    expect(dob.valid).toBe(true);
  });

  it('should detect Medicare age (65+)', () => {
    fixture.detectChanges();
    const oldDate = new Date();
    oldDate.setFullYear(oldDate.getFullYear() - 70);
    component.form.controls.dateOfBirth.setValue(oldDate);
    // Trigger change detection to process valueChanges subscription
    fixture.detectChanges();
    // Medicare age is set via subscription; check after tick
    expect(component.isMedicareAge()).toBe(true);
  });

  // ─── Concierge Toggle ─────────────────────────────────────

  it('should require conciergeAmount when concierge is 1', () => {
    fixture.detectChanges();
    component.form.controls.concierge.setValue(1);
    fixture.detectChanges();
    expect(component.form.controls.conciergeAmount.hasError('required')).toBe(true);
  });

  it('should clear conciergeAmount validation when concierge is 0', () => {
    fixture.detectChanges();
    component.form.controls.concierge.setValue(1);
    fixture.detectChanges();
    component.form.controls.concierge.setValue(0);
    fixture.detectChanges();
    expect(component.form.controls.conciergeAmount.valid).toBe(true);
  });

  // ─── MAGI Tiers ───────────────────────────────────────────

  it('should load MAGI tiers on filing status change', () => {
    fixture.detectChanges();
    component.onFilingStatusChange('MARRIED_FILING_JOINTLY');
    expect(countyMock.getMagiTiers).toHaveBeenCalledWith(
      'MARRIED_FILING_JOINTLY',
      component.form.controls.coverageYear.value,
    );
  });

  it('should clear MAGI tiers when no filing status', () => {
    fixture.detectChanges();
    component.onFilingStatusChange('');
    expect(component.magiTiers()).toEqual([]);
  });

  // ─── County Change ────────────────────────────────────────

  it('should set county and countyCode on county change', () => {
    fixture.detectChanges();
    component['allCountyEntries'].set([
      { countyCode: '12086', countyName: 'Miami-Dade', city: 'Miami', state: 'FL', latitude: 25.7, longitude: -80.2 } as CountyCodeEntry,
      { countyCode: '12086', countyName: 'Miami-Dade', city: 'Hialeah', state: 'FL', latitude: 25.8, longitude: -80.3 } as CountyCodeEntry,
    ]);
    component.onCountyChange('12086');
    expect(component.form.controls.county.value).toBe('Miami-Dade');
    expect(component.form.controls.countyCode.value).toBe('12086');
    expect(component.cities().length).toBe(2);
  });

  // ─── hasUnsavedChanges ────────────────────────────────────

  it('should detect unsaved changes when form is dirty', () => {
    fixture.detectChanges();
    component.form.markAsDirty();
    expect(component.hasUnsavedChanges()).toBe(true);
  });

  it('should detect no unsaved changes when form is pristine', () => {
    fixture.detectChanges();
    expect(component.hasUnsavedChanges()).toBe(false);
  });

  // ─── Static Data ──────────────────────────────────────────

  it('should have health condition options', () => {
    expect(component.healthConditions.length).toBe(5);
    expect(component.healthConditions[0].value).toBe(1);
    expect(component.healthConditions[4].value).toBe(5);
  });

  it('should have gender options', () => {
    expect(component.genderOptions.length).toBe(2);
  });

  it('should have yesNo options', () => {
    expect(component.yesNoOptions.length).toBe(2);
  });

  // ─── Coverage Year ────────────────────────────────────────

  it('should auto-set coverage year based on current month', () => {
    fixture.detectChanges();
    const year = new Date().getFullYear();
    const month = new Date().getMonth();
    const expected = (month >= 9 && month <= 11) ? year + 1 : year;
    expect(component.form.controls.coverageYear.value).toBe(expected);
  });

  // ─── Prefill Data ─────────────────────────────────────────

  it('should apply pending prefill data on init', () => {
    profileMock.pendingPrefill.set({
      firstName: 'Jane',
      lastName: 'Smith',
      zipCode: '90210',
    });
    fixture.detectChanges();
    expect(component.form.controls.firstName.value).toBe('Jane');
    expect(component.form.controls.lastName.value).toBe('Smith');
    expect(profileMock.pendingPrefill()).toBeNull(); // consumed
  });
});
