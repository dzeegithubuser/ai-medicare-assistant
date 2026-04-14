import { Component, inject, signal, effect, OnInit, OnDestroy, Injector } from '@angular/core';
import { Router } from '@angular/router';
import { ReactiveFormsModule, FormBuilder, Validators, AbstractControl, ValidationErrors } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatRadioModule } from '@angular/material/radio';
import { MatIconModule } from '@angular/material/icon';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { Subject, debounceTime, distinctUntilChanged, takeUntil } from 'rxjs';
import { ProfileService } from '../services/profile.service';
import { ReferenceDataService } from '../services/reference-data.service';
import { CountyLookupService, CountyCodeEntry } from '../services/county-lookup.service';
import { DrugStateService } from '../services/drug-state.service';
import { ChatWizardService } from '../services/chat-wizard.service';
import { ChatProfileEditFlowService } from '../services/chat-profile-edit-flow.service';
import { ProfileDto, LabelValuePair } from '../models/profile.model';
import { AppRoutes } from '../app-routes.const';

@Component({
  selector: 'app-user-profile',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatFormFieldModule, MatInputModule, MatSelectModule, MatRadioModule,
    MatIconModule,
    MatDatepickerModule, MatNativeDateModule, MatSnackBarModule
  ],
  templateUrl: './user-profile.component.html',
  styleUrl: './user-profile.component.scss'
})
export class UserProfileComponent implements OnInit, OnDestroy {
  private fb = inject(FormBuilder);
  private profileService = inject(ProfileService);
  private snackBar = inject(MatSnackBar);
  private countyService = inject(CountyLookupService);
  private router = inject(Router);
  readonly refData = inject(ReferenceDataService);
  private drugState = inject(DrugStateService);
  private wizard = inject(ChatWizardService);
  private profileEditFlow = inject(ChatProfileEditFlowService);
  private injector = inject(Injector);

  private destroy$ = new Subject<void>();
  private lastHandledChatSaveId = this.profileService.chatSaveRequestId();
  private lastHandledChatDiscardId = this.profileService.chatDiscardRequestId();

  saving = signal(false);
  isEditMode = signal(true);
  loadingCounty = signal(false);
  isMedicareAge = signal(false);

  // City / County dropdown data
  cities = signal<string[]>([]);
  counties = signal<CountyCodeEntry[]>([]);
  allCountyEntries = signal<CountyCodeEntry[]>([]);
  zipLookupDone = signal(false);

  // MAGI tier options (dependent on taxFilingStatus + coverageYear)
  magiTiers = signal<LabelValuePair[]>([]);
  loadingMagiTiers = signal(false);

  // Coverage year options (month-based rule)
  coverageYears = signal<number[]>([]);
  showCoverageYear = signal(false);

  // Health condition options
  readonly healthConditions = [
    { label: "Best Health", value: 1 },
		{ label: "Good Health", value: 2 },
		{ label: "Moderate Health", value: 3 },
		{ label: "Poor Health", value: 4 },
		{ label: "Sick", value: 5 }
  ];

  readonly genderOptions = [
    { label: 'Male', value: 'M' },
    { label: 'Female', value: 'F' }
  ];

  readonly yesNoOptions = [
    { label: 'No', value: 0 },
    { label: 'Yes', value: 1 }
  ];

  private readonly namePattern = /^[A-Za-z]+([' -][A-Za-z]+)*$/;

  form = this.fb.group({
    firstName: ['', [Validators.required, Validators.pattern(this.namePattern)]],
    lastName: ['', [Validators.required, Validators.pattern(this.namePattern)]],
    coverageYear: [null as number | null, Validators.required],
    healthCondition: [1, Validators.required],
    taxFilingStatus: ['MARRIED_FILING_JOINTLY', Validators.required],
    magiTier: ['', Validators.required],
    gender: ['F', Validators.required],
    tobaccoStatus: [0, Validators.required],
    dateOfBirth: [null as Date | null, [Validators.required, this.ageValidator(18)]],
    concierge: [0, Validators.required],
    conciergeAmount: [null as number | null],
    alternateEmail: ['', Validators.email],
    alternateMobile: [''],
    lifeExpectancy: [95, [Validators.required, Validators.min(65), Validators.max(120)]],
    // Address fields
    addressLine1: ['', Validators.required],
    city: ['', Validators.required],
    state: ['', Validators.required],
    zipCode: ['', [Validators.required, Validators.pattern(/^\d{5}(-\d{4})?$/)]],
    county: ['', Validators.required],
    countyCode: ['', Validators.required]
  });

  ngOnInit() {
    this.refData.load();
    this.isEditMode.set(true);
    this.computeCoverageYears();

    // Load existing profile data
    const resp = this.profileService.profile();
    if (resp?.profile) {
      const p = resp.profile;
      // Convert dateOfBirth string → Date for the datepicker
      const patchData: any = { ...p };
      if (p.dateOfBirth) {
        patchData.dateOfBirth = new Date(p.dateOfBirth + 'T00:00:00');
        this.checkMedicareAge(patchData.dateOfBirth);
      }
      this.form.patchValue(patchData);
      this.form.markAsPristine();

      if (p.zipCode) this.fetchCountyData(p.zipCode, p.countyCode, p.city);
    }

    // Apply chat-intent pre-fill data if present
    const prefill = this.profileService.pendingPrefill();
    if (prefill) {
      const patch: Record<string, unknown> = {};
      for (const [key, value] of Object.entries(prefill)) {
        if (key === 'dateOfBirth' && typeof value === 'string') {
          patch[key] = new Date(value + 'T00:00:00');
        } else {
          patch[key] = value;
        }
      }
      this.form.patchValue(patch);
      this.profileService.pendingPrefill.set(null); // consume and clear
      this.isEditMode.set(true);

      // Trigger cascading lookups for extracted fields
      if (prefill['zipCode'] && typeof prefill['zipCode'] === 'string') {
        this.fetchCountyData(prefill['zipCode'] as string);
      }
      if (patch['dateOfBirth'] instanceof Date) {
        this.checkMedicareAge(patch['dateOfBirth'] as Date);
      }
      if (prefill['taxFilingStatus']) {
        this.onFilingStatusChange(prefill['taxFilingStatus'] as string);
      }
    }

    // Load MAGI tiers for the current filing status (covers both default and loaded profile)
    const filingStatus = this.form.controls.taxFilingStatus.value;
    if (filingStatus) this.onFilingStatusChange(filingStatus);

    // Watch ZIP code → debounce → lookup county data
    this.form.controls.zipCode.valueChanges.pipe(
      debounceTime(500),
      distinctUntilChanged(),
      takeUntil(this.destroy$)
    ).subscribe(zip => {
      if (zip && /^\d{5}(-\d{4})?$/.test(zip)) {
        this.fetchCountyData(zip);
      } else {
        this.resetAddressLookup();
      }
    });

    // Watch taxFilingStatus → update MAGI tiers
    this.form.controls.taxFilingStatus.valueChanges.pipe(
      takeUntil(this.destroy$)
    ).subscribe(status => {
      this.onFilingStatusChange(status ?? '');
    });

    // Watch concierge → toggle conciergeAmount validation
    this.form.controls.concierge.valueChanges.pipe(
      takeUntil(this.destroy$)
    ).subscribe(val => {
      const amountCtrl = this.form.controls.conciergeAmount;
      if (val === 1) {
        amountCtrl.setValidators([Validators.required, Validators.min(0)]);
      } else {
        amountCtrl.clearValidators();
        amountCtrl.setValue(null);
      }
      amountCtrl.updateValueAndValidity();
    });

    // Watch dateOfBirth → Medicare age hint
    this.form.controls.dateOfBirth.valueChanges.pipe(
      takeUntil(this.destroy$)
    ).subscribe(val => {
      if (val) this.checkMedicareAge(val);
    });

    // Publish initial missing required fields for the chat profile fill feature
    this.updateMissingFields();

    // Watch all form changes to keep missing fields up-to-date
    this.form.valueChanges.pipe(
      debounceTime(300),
      takeUntil(this.destroy$)
    ).subscribe(() => {
      this.updateMissingFields();
      if (this.form.dirty) {
        this.profileEditFlow.hasUnsavedProfileChanges.set(true);
      }
    });

    // Watch for chat-extracted profile data and patch the form
    effect(() => {
      const data = this.profileService.pendingChatProfileData();
      if (!data) return;

      const patch: Record<string, unknown> = {};
      for (const [key, value] of Object.entries(data)) {
        if (key === 'dateOfBirth' && typeof value === 'string') {
          patch[key] = new Date(value + 'T00:00:00');
        } else {
          patch[key] = value;
        }
      }

      this.form.patchValue(patch);
      this.profileService.pendingChatProfileData.set(null);

      // If ZIP was extracted, trigger county/city lookup
      if (data['zipCode'] && typeof data['zipCode'] === 'string') {
        this.fetchCountyData(data['zipCode'] as string);
      }

      // If date of birth was extracted, check Medicare age
      if (patch['dateOfBirth'] instanceof Date) {
        this.checkMedicareAge(patch['dateOfBirth'] as Date);
      }

      // If tax filing status was extracted, trigger MAGI tier lookup
      if (data['taxFilingStatus']) {
        this.onFilingStatusChange(data['taxFilingStatus'] as string);
      }

      this.updateMissingFields();
    }, { injector: this.injector });

    // Analysis shell stepper: Profile is step 1 when embedded under /analysis/profile
    if (this.router.url.includes(AppRoutes.abs.PROFILE)) {
      this.drugState.currentStep.set(1);
    }

    // Profile is always editable in this flow.
    this.form.enable({ emitEvent: false });

    // Support chat-triggered profile save.
    effect(() => {
      const requestId = this.profileService.chatSaveRequestId();
      if (requestId === 0 || requestId === this.lastHandledChatSaveId) return;
      this.lastHandledChatSaveId = requestId;
      this.save();
    }, { injector: this.injector });

    // Support chat-triggered discard of unsaved profile edits.
    effect(() => {
      const requestId = this.profileService.chatDiscardRequestId();
      if (requestId === 0 || requestId === this.lastHandledChatDiscardId) return;
      this.lastHandledChatDiscardId = requestId;
      this.discardUnsavedChanges();
    }, { injector: this.injector });
  }

  ngOnDestroy() {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private readonly REQUIRED_PROFILE_FIELDS = [
    'firstName', 'lastName', 'dateOfBirth', 'gender', 'tobaccoStatus',
    'healthCondition', 'taxFilingStatus', 'coverageYear', 'magiTier', 'zipCode', 'addressLine1'
  ];

  private updateMissingFields(): void {
    const missing = this.REQUIRED_PROFILE_FIELDS.filter(field => {
      const value = this.form.get(field)?.value;
      return value === null || value === undefined || value === '';
    });
    this.profileService.missingRequiredFields.set(missing);
  }

  private computeCoverageYears() {
    const now = new Date();
    const year = now.getFullYear();
    const month = now.getMonth(); // 0-based: 0=Jan, 9=Oct, 10=Nov, 11=Dec

    // Coverage year is system-managed and non-editable in UI.
    this.showCoverageYear.set(false);
    if (month >= 9 && month <= 11) {
      // December → auto-set to upcoming year, hide picker
      this.form.controls.coverageYear.setValue(year + 1);
    } else {
      // January–September → auto-set to current year, hide picker
      this.form.controls.coverageYear.setValue(year);
    }
  }

  private ageValidator(minAge: number) {
    return (control: AbstractControl): ValidationErrors | null => {
      if (!control.value) return null;
      const dob = control.value instanceof Date ? control.value : new Date(control.value);
      if (isNaN(dob.getTime())) return null;
      const today = new Date();
      let age = today.getFullYear() - dob.getFullYear();
      const m = today.getMonth() - dob.getMonth();
      if (m < 0 || (m === 0 && today.getDate() < dob.getDate())) age--;
      return age >= minAge ? null : { minAge: { required: minAge, actual: age } };
    };
  }

  private checkMedicareAge(dob: Date | string) {
    const d = dob instanceof Date ? dob : new Date(dob);
    if (isNaN(d.getTime())) return;
    const today = new Date();
    let age = today.getFullYear() - d.getFullYear();
    const m = today.getMonth() - d.getMonth();
    if (m < 0 || (m === 0 && today.getDate() < d.getDate())) age--;
    this.isMedicareAge.set(age >= 65);
  }

  onFilingStatusChange(status: string) {
    const coverageYear = this.form.controls.coverageYear.value;
    if (!status || !coverageYear) {
      this.magiTiers.set([]);
      return;
    }
    this.loadingMagiTiers.set(true);
    this.countyService.getMagiTiers(status, coverageYear).subscribe({
      next: (tiers) => {
        this.magiTiers.set(tiers);
        const currentTier = this.form.controls.magiTier.value;
        if (currentTier && !tiers.some(t => String(t.value) === currentTier)) {
          this.form.controls.magiTier.setValue('');
        }
        this.loadingMagiTiers.set(false);
      },
      error: () => {
        this.magiTiers.set([]);
        this.loadingMagiTiers.set(false);
      }
    });
  }

  // ── ZIP Code → County/City/State Lookup ──

  private fetchCountyData(zipCode: string, savedCountyCode?: string, savedCity?: string) {
    this.loadingCounty.set(true);
    this.zipLookupDone.set(false);
    // Reset dependent fields
    this.form.patchValue({ state: '', city: '', county: '', countyCode: '' });
    this.cities.set([]);
    this.counties.set([]);
    this.allCountyEntries.set([]);

    this.countyService.getCountyCodeList(zipCode).subscribe({
      next: (entries) => {
        if (entries.length === 0) {
          this.loadingCounty.set(false);
          this.zipLookupDone.set(true);
          this.snackBar.open('No data found for this ZIP code.', 'OK', { duration: 3000 });
          return;
        }

        this.allCountyEntries.set(entries);

        // Auto-populate state from first entry (resolve full name from reference data)
        const stateCode = entries[0].state;
        if (stateCode) {
          const stateLabel = this.resolveStateName(stateCode);
          this.form.patchValue({ state: stateLabel });
        }

        // Populate county dropdown
        const uniqueCounties = this.deduplicateCounties(entries);
        this.counties.set(uniqueCounties);

        // Auto-select county if only one
        if (uniqueCounties.length === 1) {
          this.form.patchValue({
            county: uniqueCounties[0].countyName,
            countyCode: uniqueCounties[0].countyCode
          });
          this.onCountyChange(uniqueCounties[0].countyCode);
        } else if (savedCountyCode) {
          // Restore saved county in edit mode
          const match = uniqueCounties.find(c => c.countyCode === savedCountyCode);
          if (match) {
            this.form.patchValue({
              county: match.countyName,
              countyCode: match.countyCode
            });
            this.onCountyChange(match.countyCode);
          }
        }

        // Restore saved city in edit mode (after county change populated the cities list)
        if (savedCity && this.cities().includes(savedCity)) {
          this.form.patchValue({ city: savedCity });
        }

        this.loadingCounty.set(false);
        this.zipLookupDone.set(true);
      },
      error: () => {
        this.counties.set([]);
        this.allCountyEntries.set([]);
        this.loadingCounty.set(false);
        this.zipLookupDone.set(true);
        this.snackBar.open('Failed to lookup ZIP code. Please try again.', 'OK', { duration: 3000 });
      }
    });
  }

  private resetAddressLookup() {
    this.cities.set([]);
    this.counties.set([]);
    this.allCountyEntries.set([]);
    this.zipLookupDone.set(false);
    this.form.patchValue({ state: '', city: '', county: '', countyCode: '' });
  }

  onCountyChange(countyCode: string) {
    const selected = this.allCountyEntries().find(e => e.countyCode === countyCode);
    if (selected) {
      this.form.patchValue({
        county: selected.countyName,
        countyCode: selected.countyCode
      });
    }
    // Filter cities for the selected county
    const filtered = this.allCountyEntries().filter(e => e.countyCode === countyCode);
    const filteredCities = [...new Set(filtered.map(e => e.city))];
    this.cities.set(filteredCities);
    if (filteredCities.length === 1) {
      this.form.patchValue({ city: filteredCities[0] });
    } else {
      this.form.patchValue({ city: '' });
    }
  }

  onCityChange(city: string) {
    const all = this.allCountyEntries();
    const filtered = all.filter(e => e.city === city);
    if (filtered.length > 0) {
      const uniqueCounties = this.deduplicateCounties(filtered);
      this.counties.set(uniqueCounties);
      if (uniqueCounties.length === 1) {
        this.form.patchValue({
          county: uniqueCounties[0].countyName,
          countyCode: uniqueCounties[0].countyCode
        });
      }
    }
  }

  private deduplicateCounties(entries: CountyCodeEntry[]): CountyCodeEntry[] {
    const seen = new Set<string>();
    return entries.filter(e => {
      if (seen.has(e.countyCode)) return false;
      seen.add(e.countyCode);
      return true;
    });
  }

  private resolveStateName(stateCode: string): string {
    const match = this.refData.usStates().find(s => s.value === stateCode);
    return match ? `${match.label}(${stateCode})` : stateCode;
  }

  // ── Save ──

  save() {
    const chatTriggeredSave = this.profileService.chatSaveInProgress();
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      const invalid = this.getInvalidFieldLabels();
      const msg = invalid.length
        ? `Cannot continue yet. Please complete/fix these fields: ${invalid.join(', ')}.`
        : 'Cannot continue yet. Please complete required profile fields.';
      this.snackBar.open(msg, 'OK', { duration: 4000 });
      if (chatTriggeredSave) {
        this.drugState.addAssistantMessage(msg);
      }
      this.profileService.chatSaveInProgress.set(false);
      this.drugState.setLoading(false);
      return;
    }
    const hasChanges = this.hasUnsavedChanges();
    if (!hasChanges) {
      this.profileService.chatSaveInProgress.set(false);
      this.drugState.setLoading(false);
      if (this.router.url.includes(AppRoutes.abs.PROFILE) || this.router.url.includes(AppRoutes.abs.LTC_PROFILE)) {
        const isLtc = this.router.url.startsWith(AppRoutes.abs.LTC);
        if (chatTriggeredSave) {
          this.drugState.addAssistantMessage(
            isLtc ? 'No profile changes detected. Continuing to care type.' : 'No profile changes detected. Continuing to drugs.'
          );
        }
        if (isLtc) {
          this.wizard.ltcProfileIntroComplete.set(true);
        } else {
          this.wizard.medicareProfileIntroComplete.set(true);
        }
        this.drugState.returnRoute.set(null);
        this.router.navigate([isLtc ? AppRoutes.abs.LTC_CARE_TYPE : AppRoutes.abs.DRUGS]);
        return;
      }
      this.snackBar.open('No profile changes to save.', 'OK', { duration: 2500 });
      return;
    }
    this.saving.set(true);
    const previousProfile = this.profileService.profile()?.profile ?? null;
    const returnTo = this.drugState.returnRoute();

    const formValue = { ...this.form.value } as any;

    // Format dateOfBirth as yyyy-MM-dd
    if (formValue.dateOfBirth instanceof Date) {
      formValue.dateOfBirth = formValue.dateOfBirth.toISOString().split('T')[0];
    }

    // Extract state code from display format "StateName(XX)" → "XX"
    if (formValue.state) {
      const codeMatch = formValue.state.match(/\(([A-Z]{2})\)$/);
      if (codeMatch) formValue.state = codeMatch[1];
    }

    // Convert empty optional strings to null for backend validation
    if (!formValue.alternateEmail) formValue.alternateEmail = null;
    if (!formValue.alternateMobile) formValue.alternateMobile = null;

    // Set latitude/longitude from the selected county entry
    const selectedEntry = this.allCountyEntries().find(
      e => e.countyCode === formValue.countyCode && e.city === formValue.city
    ) ?? this.allCountyEntries().find(e => e.countyCode === formValue.countyCode);
    formValue.latitude = selectedEntry?.latitude ?? null;
    formValue.longitude = selectedEntry?.longitude ?? null;

    this.profileService.saveProfile(formValue as ProfileDto).subscribe({
      next: () => {
        this.saving.set(false);
        this.profileService.chatSaveInProgress.set(false);
        this.drugState.setLoading(false);
        this.snackBar.open('Profile saved successfully!', 'OK', { duration: 3000 });
        this.drugState.addSystemMessage('Profile saved');
        if (chatTriggeredSave) {
          this.drugState.addAssistantMessage('Profile saved successfully.');
        }
        if (returnTo?.startsWith(AppRoutes.abs.MEDICARE_ANALYSIS) && this.hasImpactfulAnalysisChanges(previousProfile, formValue)) {
          this.drugState.invalidateAfterProfileChange();
          this.drugState.addAssistantMessage(
            'Your profile changes affect analysis assumptions. I kept your drugs and cleared downstream selections (pharmacy/plans/cost). Please continue from pharmacies.'
          );
        }
        this.form.markAsPristine();
        this.profileEditFlow.hasUnsavedProfileChanges.set(false);
        if (this.router.url.includes(AppRoutes.abs.PROFILE) || this.router.url.includes(AppRoutes.abs.LTC_PROFILE)) {
          const isLtc = this.router.url.startsWith(AppRoutes.abs.LTC);
          const nextRoute = isLtc ? AppRoutes.abs.LTC_CARE_TYPE : AppRoutes.abs.DRUGS;
          if (isLtc) {
            this.wizard.ltcProfileIntroComplete.set(true);
          } else {
            this.wizard.medicareProfileIntroComplete.set(true);
          }
          this.drugState.returnRoute.set(null);
          this.router.navigate([nextRoute]);
          return;
        }
        this.navigateBack();
      },
      error: () => {
        this.saving.set(false);
        this.profileService.chatSaveInProgress.set(false);
        this.drugState.setLoading(false);
        this.snackBar.open('Failed to save profile. Please try again.', 'OK', { duration: 3000 });
      }
    });
  }

  closeEditPanel() {
    this.navigateBack();
  }

  enterEditMode() {
    this.isEditMode.set(true);
    this.form.enable({ emitEvent: false });
  }

  hasUnsavedChanges(): boolean {
    return this.form.dirty || this.profileEditFlow.hasUnsavedProfileChanges();
  }

  discardUnsavedChanges() {
    const p = this.profileService.profile()?.profile;
    if (p) {
      const patchData: any = { ...p };
      if (p.dateOfBirth) {
        patchData.dateOfBirth = new Date(p.dateOfBirth + 'T00:00:00');
        this.checkMedicareAge(patchData.dateOfBirth);
      }
      this.form.patchValue(patchData, { emitEvent: false });
      if (p.zipCode) this.fetchCountyData(p.zipCode, p.countyCode, p.city);
      if (p.taxFilingStatus) this.onFilingStatusChange(p.taxFilingStatus);
    }
    this.profileService.pendingChatProfileData.set(null);
    this.profileService.pendingPrefill.set(null);
    this.profileService.chatSaveInProgress.set(false);
    this.isEditMode.set(true);
    this.form.enable({ emitEvent: false });
    this.form.markAsPristine();
    this.profileEditFlow.hasUnsavedProfileChanges.set(false);
    this.updateMissingFields();
    this.snackBar.open('Unsaved profile changes discarded.', 'OK', { duration: 2500 });
  }

  private navigateBack(): void {
    const returnTo = this.drugState.returnRoute();
    this.drugState.returnRoute.set(null);
    this.router.navigate([returnTo ?? AppRoutes.abs.MEDICARE_ANALYSIS]);
  }

  private hasImpactfulAnalysisChanges(
    previous: ProfileDto | null,
    nextFormValue: Partial<ProfileDto>
  ): boolean {
    if (!previous) return false;
    const impactfulFields: (keyof ProfileDto)[] = [
      'dateOfBirth',
      'gender',
      'tobaccoStatus',
      'healthCondition',
      'taxFilingStatus',
      'magiTier',
      'coverageYear',
      'lifeExpectancy',
      'concierge',
      'conciergeAmount',
      'zipCode',
      'state',
      'city',
      'county',
      'countyCode',
      'addressLine1',
      'latitude',
      'longitude'
    ];

    for (const field of impactfulFields) {
      const before = previous[field];
      const after = nextFormValue[field];
      if (!this.valuesEqual(before, after)) return true;
    }
    return false;
  }

  private valuesEqual(a: unknown, b: unknown): boolean {
    if (a == null && b == null) return true;
    return String(a ?? '').trim() === String(b ?? '').trim();
  }

  private getInvalidFieldLabels(): string[] {
    const fieldLabels: Record<string, string> = {
      firstName: 'First name',
      lastName: 'Last name',
      dateOfBirth: 'Date of birth',
      gender: 'Gender',
      tobaccoStatus: 'Tobacco use',
      healthCondition: 'Health condition',
      taxFilingStatus: 'Tax filing status',
      magiTier: 'MAGI tier',
      zipCode: 'ZIP code',
      addressLine1: 'Address line 1',
      state: 'State',
      county: 'County',
      countyCode: 'County code',
      city: 'City',
      lifeExpectancy: 'Life expectancy',
      conciergeAmount: 'Concierge amount',
    };
    return Object.keys(this.form.controls)
      .filter((k) => this.form.get(k)?.invalid)
      .map((k) => fieldLabels[k] ?? k);
  }
}
