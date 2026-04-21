import { Injectable, signal, effect } from '@angular/core';

export type AppTheme = 'navy' | 'lavender' | 'teal';

export interface ThemeMeta {
  label: string;
  primaryColor: string;
  bgColor: string;
}

const THEMES: AppTheme[] = ['navy', 'lavender', 'teal'];
const STORAGE_KEY = 'aimedicare_theme';
const DEFAULT_THEME: AppTheme = 'navy';

export const THEME_META: Record<AppTheme, ThemeMeta> = {
  navy:     { label: 'Navy & Gold',     primaryColor: '#1E3A5F', bgColor: '#F8F6F1' },
  lavender: { label: 'Lavender Calm',   primaryColor: '#5B4A8A', bgColor: '#F5F3FA' },
  teal:     { label: 'Teal Medical',    primaryColor: '#0D6E6E', bgColor: '#F0FAFA' },
};

@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly _theme = signal<AppTheme>(this.loadPersistedTheme());

  readonly theme = this._theme.asReadonly();
  readonly themes: AppTheme[] = THEMES;
  readonly meta = THEME_META;

  constructor() {
    effect(() => {
      const t = this._theme();
      document.documentElement.setAttribute('data-theme', t);
      localStorage.setItem(STORAGE_KEY, t);
    });
  }

  setTheme(theme: AppTheme): void {
    this._theme.set(theme);
  }

  private loadPersistedTheme(): AppTheme {
    const stored = localStorage.getItem(STORAGE_KEY) as AppTheme;
    return (THEMES as string[]).includes(stored) ? stored : DEFAULT_THEME;
  }
}
