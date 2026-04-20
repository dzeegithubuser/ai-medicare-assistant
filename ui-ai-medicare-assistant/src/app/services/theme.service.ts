import { Injectable, signal, effect } from '@angular/core';

export type AppTheme = 'blue' | 'warm' | 'green';

export interface ThemeMeta {
  label: string;
  primaryColor: string;
  bgColor: string;
}

const THEMES: AppTheme[] = ['blue', 'warm', 'green'];
const STORAGE_KEY = 'aimedicare_theme';
const DEFAULT_THEME: AppTheme = 'blue';

export const THEME_META: Record<AppTheme, ThemeMeta> = {
  blue:  { label: 'Blue',  primaryColor: '#0B5FFF', bgColor: '#FFFFFF' },
  warm:  { label: 'Warm',  primaryColor: '#8B5E3C', bgColor: '#FAF3E0' },
  green: { label: 'Green', primaryColor: '#2E7D32', bgColor: '#F0F7F4' },
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
