import { Injectable, inject, signal } from '@angular/core';
import { ChatProfileService } from './chat-profile.service';
import { ProfileService } from './profile.service';
import { CountyLookupService } from './county-lookup.service';
import { ChatIntentService } from './chat-intent.service';
import { DrugStateService } from './drug-state.service';
import { Router } from '@angular/router';
import { LabelValuePair } from '../models/profile.model';
import { APP_MESSAGES, PROFILE_MESSAGES } from '../constants/chat-messages';
import { AppRoutes } from '../app-routes.const';

@Injectable({ providedIn: 'root' })
export class ChatProfileEditFlowService {
  private state = inject(DrugStateService);
  private profileService = inject(ProfileService);
  private countyLookup = inject(CountyLookupService);
  private chatIntentSvc = inject(ChatIntentService);
  private chatProfile = inject(ChatProfileService);
  private router = inject(Router);

  readonly pendingProfileUpdate = signal<Record<string, unknown> | null>(null);
  readonly pendingTaxFilingChoice = signal(false);
  readonly pendingMagiTierChoices = signal<LabelValuePair[]>([]);
  readonly hasUnsavedProfileChanges = signal(false);

  resolvePendingProfileUpdate(accept: boolean): boolean {
    const profile = this.pendingProfileUpdate();
    if (!profile) return false;

    this.pendingProfileUpdate.set(null);
    this.pendingTaxFilingChoice.set(false);
    this.pendingMagiTierChoices.set([]);
    if (accept) {
      this.profileService.pendingChatProfileData.set(profile);
      this.state.addAssistantMessage(APP_MESSAGES.PROFILE_UPDATED_IN_FORM);
      this.hasUnsavedProfileChanges.set(true);
    } else {
      this.state.addAssistantMessage(APP_MESSAGES.GENERIC_CANCELLED);
      this.hasUnsavedProfileChanges.set(false);
    }
    this.state.setLoading(false);
    return true;
  }

  triggerDirectProfileSave(): void {
    this.profileService.requestSaveFromChat();
  }

  resolveProfileEditIntentWithAi(text: string): void {
    this.chatIntentSvc.classify(text, this.profileService.isProfileComplete(), this.router.url).subscribe({
      next: (result) => {
        const lower = text.toLowerCase();
        const looksLikeDiscard =
          /\b(canc|cancel|discard|abort)\b/.test(lower) &&
          /\b(edit|change|profile)\b/.test(lower);
        if (result.intent === 'ACTION_RESET_ANALYSIS' || (result.intent === 'UNKNOWN' && looksLikeDiscard)) {
          this.discardPendingProfileChanges();
          this.state.setLoading(false);
          return;
        }
        this.state.setLoading(false);
        this.state.addAssistantMessage(APP_MESSAGES.PROFILE_INTENT_HELP);
      },
      error: () => {
        this.state.setLoading(false);
        this.state.addAssistantMessage(APP_MESSAGES.PROFILE_INTENT_UNCLEAR);
      },
    });
  }

  discardPendingProfileChanges(): void {
    this.pendingProfileUpdate.set(null);
    this.pendingTaxFilingChoice.set(false);
    this.pendingMagiTierChoices.set([]);
    this.hasUnsavedProfileChanges.set(false);
    this.profileService.pendingChatProfileData.set(null);
    this.profileService.pendingPrefill.set(null);
    this.profileService.requestDiscardFromChat();
    this.state.addAssistantMessage(APP_MESSAGES.PROFILE_DISCARDED);
  }

  /**
   * @param onEmptyExtraction Optional callback invoked when AI extraction returns no
   *   profile fields. Used by the profile-page DRUG_INPUT handler: if the AI misclassifies
   *   a profile phrase (e.g. "magitier is 150") as a drug, extraction still succeeds and
   *   applies the update; but if the text truly is a drug name (e.g. "add eliquis"),
   *   extraction returns empty and the callback redirects to cross-page drug search.
   */
  routeToProfileExtraction(text: string, onEmptyExtraction?: () => void): boolean {
    const url = this.router.url;
    if (!(url.startsWith('/profile') || url.startsWith(AppRoutes.abs.PROFILE))) return false;

    const missingFields = this.profileService.isProfileComplete()
      ? []
      : this.profileService.missingRequiredFields();
    this.chatProfile.extractProfile({ message: text, missingFields }).subscribe({
      next: (res) => {
        this.state.setLoading(false);
        if (res.extractedFields && Object.keys(res.extractedFields).length > 0) {
          const extracted = { ...res.extractedFields };
          delete extracted['coverageYear'];
          const askedTaxFiling = /tax|filing|filling|joint|individ|single|married/i.test(text);
          const rawTax = extracted['taxFilingStatus'];
          const normalizedTax =
            this.normalizeTaxFilingStatus(rawTax) ??
            this.inferTaxFilingStatusFromText(text);
          const needsTaxChoice = askedTaxFiling && (!rawTax || !normalizedTax);

          if (normalizedTax) {
            extracted['taxFilingStatus'] = normalizedTax;
          } else if (rawTax) {
            delete extracted['taxFilingStatus'];
          }

          this.pendingTaxFilingChoice.set(needsTaxChoice);
          this.pendingMagiTierChoices.set([]);

          if (needsTaxChoice) {
            this.pendingProfileUpdate.set(extracted);
            this.state.addAssistantMessage(
              'I could not determine your tax filing status clearly. Please choose one option below.'
            );
            return;
          }

          const rawMagi = extracted['magiTier'];
          if (rawMagi !== undefined && rawMagi !== null && String(rawMagi).trim() !== '') {
            const profile = this.profileService.profile()?.profile;
            const filingStatus = String(extracted['taxFilingStatus'] ?? profile?.taxFilingStatus ?? '').trim();
            const coverageYear = Number(extracted['coverageYear'] ?? profile?.coverageYear ?? 0);

            if (filingStatus && coverageYear > 0) {
              this.pendingProfileUpdate.set(extracted);
              this.countyLookup.getMagiTiers(filingStatus, coverageYear).subscribe({
                next: (tiers) => {
                  const currentMagi = String(this.pendingProfileUpdate()?.['magiTier'] ?? '').trim();
                  const normalizedMagi = this.normalizeMagiTierFromOptions(currentMagi, tiers);
                  if (normalizedMagi) {
                    const next = { ...(this.pendingProfileUpdate() ?? {}) };
                    next['magiTier'] = normalizedMagi;
                    this.pendingProfileUpdate.set(next);
                  }

                  if (!normalizedMagi && tiers.length > 0) {
                    const next = { ...(this.pendingProfileUpdate() ?? {}) };
                    delete next['magiTier'];
                    this.pendingProfileUpdate.set(next);
                    this.pendingMagiTierChoices.set(tiers);
                    this.state.addAssistantMessage(
                      'The MAGI tier you provided is not valid for this filing status/year. Please choose one from the list below.'
                    );
                    return;
                  }
                  this.applyExtractedProfileUpdate(this.pendingProfileUpdate() ?? extracted);
                },
                error: () => this.applyExtractedProfileUpdate(this.pendingProfileUpdate() ?? extracted),
              });
              return;
            }
          }

          this.applyExtractedProfileUpdate(extracted);
        } else {
          this.pendingTaxFilingChoice.set(false);
          this.pendingMagiTierChoices.set([]);
          if (onEmptyExtraction) {
            onEmptyExtraction();
          } else {
            this.state.addAssistantMessage(res.reply);
          }
        }
      },
      error: () => {
        this.state.setLoading(false);
        this.state.addAssistantMessage(
          'Sorry, I couldn\'t process that. Try telling me your profile details like: "I\'m John Smith, male, born 01/15/1955, ZIP 80113"'
        );
      },
    });
    return true;
  }

  applyTaxFilingChoice(choice: 'MARRIED_FILING_JOINTLY' | 'FILING_INDIVIDUALLY'): void {
    const current = this.pendingProfileUpdate() ?? {};
    const nextProfile = { ...current, taxFilingStatus: choice };
    this.pendingProfileUpdate.set(nextProfile);
    this.pendingTaxFilingChoice.set(false);
    this.applyExtractedProfileUpdate(nextProfile);
  }

  applyMagiTierChoice(value: number): void {
    const current = this.pendingProfileUpdate() ?? {};
    const nextProfile = { ...current, magiTier: String(value) };
    this.pendingProfileUpdate.set(nextProfile);
    this.pendingMagiTierChoices.set([]);
    this.applyExtractedProfileUpdate(nextProfile);
  }

  private applyExtractedProfileUpdate(extracted: Record<string, unknown>): void {
    this.pendingProfileUpdate.set(null);
    this.pendingTaxFilingChoice.set(false);
    this.pendingMagiTierChoices.set([]);
    this.profileService.pendingChatProfileData.set(extracted);
    const fields = Object.entries(extracted).map(([k, v]) => `- **${k}**: ${v}`).join('\n');
    this.state.addAssistantMessage(
      `${PROFILE_MESSAGES.UPDATED_FIELDS_PREFIX}\n\n${fields}\n\n${PROFILE_MESSAGES.UPDATED_FIELDS_SUFFIX}`
    );
    this.hasUnsavedProfileChanges.set(true);
  }

  private normalizeTaxFilingStatus(value: unknown): 'MARRIED_FILING_JOINTLY' | 'FILING_INDIVIDUALLY' | null {
    if (typeof value !== 'string') return null;
    const v = value.trim().toUpperCase().replace(/\s+/g, '_');
    if (v === 'MARRIED_FILING_JOINTLY' || v === 'JOINTLY' || v === 'JOINT' || v === 'MARRIED') {
      return 'MARRIED_FILING_JOINTLY';
    }
    if (
      v === 'FILING_INDIVIDUALLY' ||
      v === 'INDIVIDUALLY' ||
      v === 'INDIVIDUAL' ||
      v === 'SINGLE' ||
      v === 'MARRIED_FILING_SEPARATELY' ||
      v === 'SEPARATE'
    ) {
      return 'FILING_INDIVIDUALLY';
    }
    return null;
  }

  private inferTaxFilingStatusFromText(text: string): 'MARRIED_FILING_JOINTLY' | 'FILING_INDIVIDUALLY' | null {
    const lower = text.toLowerCase();
    if (/\b(joint|jointly|married)\b/.test(lower)) {
      return 'MARRIED_FILING_JOINTLY';
    }
    if (/\b(individual|individually|single|separate|separately)\b/.test(lower)) {
      return 'FILING_INDIVIDUALLY';
    }
    return null;
  }

  private normalizeMagiTierFromOptions(rawValue: string, tiers: LabelValuePair[]): string | null {
    const raw = rawValue.trim();
    if (!raw) return null;

    const byValue = tiers.find(t => String(t.value) === raw);
    if (byValue) return String(byValue.value);

    const rawLower = raw.toLowerCase();
    const byLabelExact = tiers.find(t => t.label.trim().toLowerCase() === rawLower);
    if (byLabelExact) return String(byLabelExact.value);

    const byLabelContains = tiers.find(t => {
      const label = t.label.trim().toLowerCase();
      return label.includes(rawLower) || rawLower.includes(label);
    });
    return byLabelContains ? String(byLabelContains.value) : null;
  }
}
