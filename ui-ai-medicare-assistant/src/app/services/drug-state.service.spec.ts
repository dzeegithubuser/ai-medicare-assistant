import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { DrugStateService } from './drug-state.service';
import { ChatSignalRService } from './chat-signal-r.service';

describe('DrugStateService', () => {
  let service: DrugStateService;

  beforeEach(() => {
    sessionStorage.clear();

    TestBed.configureTestingModule({
      providers: [
        DrugStateService,
        { provide: ChatSignalRService, useValue: { sendUiState: vi.fn() } },
        { provide: Router, useValue: { url: '/analysis/fp-drugs' } },
      ],
    });

    service = TestBed.inject(DrugStateService);
  });

  afterEach(() => sessionStorage.clear());

  // ─── Initial State ─────────────────────────────────────────────

  it('should start with empty messages', () => {
    expect(service.messages()).toEqual([]);
  });

  it('should start on step 1', () => {
    expect(service.currentStep()).toBe(1);
  });

  it('should not be loading initially', () => {
    expect(service.isLoading()).toBe(false);
  });

  // ─── Message Management ────────────────────────────────────────

  it('should add a user message', () => {
    service.addUserMessage('hello');

    const msgs = service.messages();
    expect(msgs.length).toBe(1);
    expect(msgs[0].role).toBe('user');
    expect(msgs[0].content).toBe('hello');
  });

  it('should add an assistant message', () => {
    service.addAssistantMessage('response');

    expect(service.messages()[0].role).toBe('assistant');
  });

  it('should add a system message', () => {
    service.addSystemMessage('info');

    expect(service.messages()[0].role).toBe('system');
  });

  it('should replace last assistant message', () => {
    service.addAssistantMessage('first');
    service.addUserMessage('user');
    service.addAssistantMessage('second');

    service.replaceLastAssistantMessage('replaced');

    const msgs = service.messages();
    expect(msgs[2].content).toBe('replaced');
    expect(msgs[0].content).toBe('first');
  });

  it('should remove assistant messages containing text', () => {
    service.addAssistantMessage('keep this');
    service.addAssistantMessage('remove this target');
    service.addUserMessage('also keep');

    service.removeAssistantMessagesContaining('target');

    const msgs = service.messages();
    expect(msgs.length).toBe(2);
    expect(msgs.every(m => !m.content.includes('target'))).toBe(true);
  });

  // ─── Hydrate Messages ──────────────────────────────────────────

  it('should hydrate messages from server', () => {
    const serverMessages = [
      { role: 'user' as const, content: 'hi', timestamp: new Date() },
      { role: 'assistant' as const, content: 'hello', timestamp: new Date() },
    ];

    service.hydrateMessagesFromServer(serverMessages);

    expect(service.messages().length).toBe(2);
  });

  // ─── Loading State ─────────────────────────────────────────────

  it('should toggle loading', () => {
    service.setLoading(true);
    expect(service.isLoading()).toBe(true);
    service.setLoading(false);
    expect(service.isLoading()).toBe(false);
  });

  // ─── Drug Suggestions ──────────────────────────────────────────

  it('should track drug suggestions', () => {
    expect(service.hasSuggestions()).toBe(false);

    service.setDrugSuggestions([{ name: 'Eliquis', type: 'brand' } as any]);

    expect(service.hasSuggestions()).toBe(true);
    expect(service.drugSuggestions().length).toBe(1);
  });

  it('should clear suggestions', () => {
    service.setDrugSuggestions([{ name: 'Test' } as any]);
    service.clearSuggestions();

    expect(service.hasSuggestions()).toBe(false);
    expect(service.isVerifyingNames()).toBe(false);
  });

  // ─── Pharmacy Toggle ───────────────────────────────────────────

  it('should toggle pharmacy selection on and off', () => {
    const pharmacy = { pharmacyNumber: '123', pharmacyName: 'CVS' } as any;

    const added = service.toggleLookupPharmacy(pharmacy);
    expect(added).toBe(true);
    expect(service.selectedLookupPharmacies().length).toBe(1);

    const removed = service.toggleLookupPharmacy(pharmacy);
    expect(removed).toBe(true);
    expect(service.selectedLookupPharmacies().length).toBe(0);
  });

  it('should reject pharmacy selection beyond max 5', () => {
    for (let i = 1; i <= 5; i++) {
      service.toggleLookupPharmacy({ pharmacyNumber: String(i), pharmacyName: `P${i}` } as any);
    }

    const result = service.toggleLookupPharmacy({ pharmacyNumber: '6', pharmacyName: 'P6' } as any);
    expect(result).toBe(false);
    expect(service.selectedLookupPharmacies().length).toBe(5);
  });

  it('should check if pharmacy is selected', () => {
    service.toggleLookupPharmacy({ pharmacyNumber: '42', pharmacyName: 'Test' } as any);

    expect(service.isLookupPharmacySelected('42')).toBe(true);
    expect(service.isLookupPharmacySelected('99')).toBe(false);
  });

  // ─── FP Active Section & Plan Selection ────────────────────────

  it('should track active section', () => {
    service.setActiveSection('partd');
    expect(service.activeSection()).toBe('partd');
  });

  it('should reset FP plan selections', () => {
    service.selectPartDPlan({ planName: 'Plan A' } as any);
    service.selectMedigapPlan({ planName: 'Medigap B' } as any);

    service.resetPlanSelections();

    expect(service.selectedPartDPlan()).toBeNull();
    expect(service.selectedMedigapPlan()).toBeNull();
    expect(service.selectedMAPlan()).toBeNull();
  });

  it('should compute hasCompletePlanSelection for partd section', () => {
    service.setActiveSection('partd');
    expect(service.hasCompletePlanSelection()).toBe(false);

    service.selectPartDPlan({ planName: 'P' } as any);
    expect(service.hasCompletePlanSelection()).toBe(false);

    service.selectMedigapPlan({ planName: 'M' } as any);
    expect(service.hasCompletePlanSelection()).toBe(true);
  });

  // ─── resetAll ──────────────────────────────────────────────────

  it('should reset all state and add reset message', () => {
    service.addUserMessage('test');
    service.setLoading(true);
    service.setActiveSection('partd');
    service.selectPartDPlan({ planName: 'Test' } as any);

    service.resetAll();

    expect(service.isLoading()).toBe(false);
    expect(service.activeSection()).toBeNull();
    expect(service.selectedPartDPlan()).toBeNull();
    expect(service.currentStep()).toBe(1);
    // resetAll adds an assistant message about reset
    const msgs = service.messages();
    expect(msgs.some(m => m.content.includes('reset'))).toBe(true);
  });

  // ─── invalidateAfterProfileChange ──────────────────────────────

  it('should clear downstream state but keep drug data', () => {
    service.setActiveSection('partd');
    service.selectPartDPlan({ planName: 'Plan' } as any);
    service.setPharmacyLookup({ pharmacies: [] } as any);
    service.setDrugSuggestions([{ name: 'Keep' } as any]);

    service.invalidateAfterProfileChange();

    // Downstream cleared
    expect(service.activeSection()).toBeNull();
    expect(service.selectedPartDPlan()).toBeNull();
    expect(service.pharmacyLookup()).toBeNull();
    // Drug suggestions are NOT cleared by invalidateAfterProfileChange
    expect(service.drugSuggestions().length).toBe(1);
  });

  // ─── clearForSignOut ───────────────────────────────────────────

  it('should wipe all state including messages', () => {
    service.addUserMessage('test');
    service.setActiveSection('ma');

    service.clearForSignOut();

    expect(service.messages()).toEqual([]);
    expect(service.activeSection()).toBeNull();
    expect(service.currentStep()).toBe(1);
  });

  // ─── Wizard Reset Trigger ──────────────────────────────────────

  it('should increment wizardResetTrigger on resetAll', () => {
    const before = service.wizardResetTrigger();
    service.resetAll();
    expect(service.wizardResetTrigger()).toBe(before + 1);
  });
});
