import { Injectable, inject, signal } from '@angular/core';
import { Subscription } from 'rxjs';
import { ChatProfileService } from './chat-profile.service';
import { ProfileService } from './profile.service';
import { CountyLookupService } from './county-lookup.service';
import { ChatIntentService } from './chat-intent.service';
import { MedicareStateService } from './drug-state.service';
import { Router } from '@angular/router';
import { LabelValuePair } from '../models/profile.model';
import { APP_MESSAGES, PROFILE_MESSAGES } from '../constants/chat-messages';
import { AppRoutes } from '../app-routes.const';

@Injectable({ providedIn: 'root' })
export class ChatProfileEditFlowService {
  private state = inject(MedicareStateService);
  private profileService = inject(ProfileService);
  private countyLookup = inject(CountyLookupService);
  private chatIntentSvc = inject(ChatIntentService);
  private chatProfile = inject(ChatProfileService);
  private router = inject(Router);

  readonly pendingProfileUpdate = signal<Record<string, unknown> | null>(null);
  readonly pendingTaxFilingChoice = signal(false);
  readonly pendingMagiTierChoices = signal<LabelValuePair[]>([]);
  readonly hasUnsavedProfileChanges = signal(false);
  private extractionSub?: Subscription;

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
    if (!(url.startsWith('/profile') || url.startsWith(AppRoutes.abs.PROFILE) || url.startsWith(AppRoutes.abs.LTC_PROFILE))) return false;

    const missingFields = this.profileService.isProfileComplete()
      ? []
      : this.profileService.missingRequiredFields();
    this.extractionSub?.unsubscribe();
    this.extractionSub = this.chatProfile.extractProfile({ message: text, missingFields }).subscribe({
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
          const rawIncome = extracted['annualIncome'];

          // ── Income → MAGI tier resolution ──────────────────────────────
          if (rawIncome !== undefined && rawIncome !== null && !rawMagi) {
            const income = Number(rawIncome);
            if (!isNaN(income) && income > 0) {
              const profile = this.profileService.profile()?.profile;
              const filingStatus = String(extracted['taxFilingStatus'] ?? profile?.taxFilingStatus ?? '').trim();
              const coverageYear = Number(extracted['coverageYear'] ?? profile?.coverageYear ?? 0);
              delete extracted['annualIncome'];

              if (filingStatus && coverageYear > 0) {
                this.pendingProfileUpdate.set(extracted);
                this.countyLookup.getMagiTiers(filingStatus, coverageYear).subscribe({
                  next: (tiers) => {
                    const matchedTier = this.findTierByIncome(income, tiers);
                    if (matchedTier) {
                      const next = { ...(this.pendingProfileUpdate() ?? {}) };
                      next['magiTier'] = matchedTier;
                      this.pendingProfileUpdate.set(next);
                      this.applyExtractedProfileUpdate(this.pendingProfileUpdate() ?? extracted);
                    } else if (tiers.length > 0) {
                      this.pendingMagiTierChoices.set(tiers);
                      this.state.addAssistantMessage(
                        `Your income of $${income.toLocaleString()} doesn't fall neatly into a single MAGI tier. Please choose the correct tier from the list below.`
                      );
                    } else {
                      this.applyExtractedProfileUpdate(this.pendingProfileUpdate() ?? extracted);
                    }
                  },
                  error: () => this.applyExtractedProfileUpdate(this.pendingProfileUpdate() ?? extracted),
                });
                return;
              } else {
                this.state.addAssistantMessage(
                  'I need your tax filing status to determine the correct MAGI tier from your income. Please provide your filing status (single, married filing jointly, or married filing separately).'
                );
                this.pendingProfileUpdate.set(extracted);
                return;
              }
            }
          }

          // ── Direct MAGI tier resolution ────────────────────────────────
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
    const suffix = this.router.url.startsWith(AppRoutes.abs.LTC_PROFILE)
      ? 'Review on the left, then click **Continue to Care Type** in the footer to save and proceed.'
      : PROFILE_MESSAGES.UPDATED_FIELDS_SUFFIX;
    this.state.addAssistantMessage(
      `${PROFILE_MESSAGES.UPDATED_FIELDS_PREFIX}\n\n${fields}\n\n${suffix}`
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

  /**
   * Parse a tier label like "$ 103K to $ 129K" or "$ 500K +" into [min, max] in dollars.
   * Returns null if the label cannot be parsed.
   */
  private parseTierRange(label: string): { min: number; max: number } | null {
    // Normalize: remove $ signs, commas, extra spaces
    const cleaned = label.replace(/\$/g, '').replace(/,/g, '').trim();
    // Match patterns like "103K to 129K", "103k to 129k", "500K +", "500K and above"
    const rangeMatch = cleaned.match(/([\d.]+)\s*([KkMm])?\s*(?:to|-)\s*([\d.]+)\s*([KkMm])?/);
    if (rangeMatch) {
      const min = this.parseIncomeValue(rangeMatch[1], rangeMatch[2]);
      const max = this.parseIncomeValue(rangeMatch[3], rangeMatch[4]);
      return (min !== null && max !== null) ? { min, max } : null;
    }
    // Open-ended upper tier: "500K +" or "500K and above"
    const openMatch = cleaned.match(/([\d.]+)\s*([KkMm])?\s*(\+|and above|or more|above)/i);
    if (openMatch) {
      const min = this.parseIncomeValue(openMatch[1], openMatch[2]);
      return min !== null ? { min, max: Infinity } : null;
    }
    // Open-ended lower tier: "Less than 103K" or "Under 103K"
    const lowerMatch = cleaned.match(/(?:less than|under|below)\s*([\d.]+)\s*([KkMm])?/i);
    if (lowerMatch) {
      const max = this.parseIncomeValue(lowerMatch[1], lowerMatch[2]);
      return max !== null ? { min: 0, max } : null;
    }
    return null;
  }

  private parseIncomeValue(numStr: string, suffix?: string): number | null {
    const num = parseFloat(numStr);
    if (isNaN(num)) return null;
    const upper = (suffix ?? '').toUpperCase();
    if (upper === 'K') return num * 1000;
    if (upper === 'M') return num * 1000000;
    return num;
  }

  /**
   * Find the MAGI tier whose income range contains the given income.
   * Returns the tier value as a string, or null if no match.
   */
  private findTierByIncome(income: number, tiers: LabelValuePair[]): string | null {
    for (const tier of tiers) {
      const range = this.parseTierRange(tier.label);
      if (range && income >= range.min && income <= range.max) {
        return String(tier.value);
      }
    }
    return null;
  }
}
