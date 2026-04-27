import { Injectable } from '@angular/core';

/** Centralised key registry — every sessionStorage key in the app lives here. */
export const SESSION_KEYS = {
  AUTH_TOKEN:       'auth_token',
  AUTH_USER:        'auth_user',
  AUTH_TOKEN_TS:    'auth_token_ts',
  DRUG_STATE:       'drug-analysis-state',
  CONFIRMED_DRUGS:  'confirmed-drugs',
  CHAT_MESSAGES:    'chat-messages-state',
  FORMULATION_SEL:  'formulation-selections',
  FP_DRUG_SEL:      'fp-drug-selections',
  DRUG_QUANTITIES:  'drug-quantities',
} as const;

/**
 * Thin wrapper around `sessionStorage` for testability and key safety.
 * Prefer injecting this service over calling `sessionStorage` directly in new code.
 */
@Injectable({ providedIn: 'root' })
export class SessionStorageService {
  get<T = string>(key: string): T | null {
    const raw = sessionStorage.getItem(key);
    if (raw === null) return null;
    try {
      return JSON.parse(raw) as T;
    } catch {
      return raw as unknown as T;
    }
  }

  getString(key: string): string | null {
    return sessionStorage.getItem(key);
  }

  set(key: string, value: unknown): void {
    sessionStorage.setItem(
      key,
      typeof value === 'string' ? value : JSON.stringify(value),
    );
  }

  remove(key: string): void {
    sessionStorage.removeItem(key);
  }

  removeMany(keys: string[]): void {
    keys.forEach((k) => sessionStorage.removeItem(k));
  }

  clear(): void {
    sessionStorage.clear();
  }
}
