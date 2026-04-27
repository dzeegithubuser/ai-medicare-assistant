import { Injectable, signal, computed, effect } from '@angular/core';

const STORAGE_KEY = 'aimedicare_font_size';
const SIZES = [16, 18, 20, 22, 24] as const;
type FontSize = (typeof SIZES)[number];

const DEFAULT_SIZE: FontSize = 24;
const MIN_SIZE = SIZES[0];
const MAX_SIZE = SIZES[SIZES.length - 1];

@Injectable({ providedIn: 'root' })
export class FontSizeService {
  private readonly _size = signal<FontSize>(this.loadPersistedSize());

  readonly size = this._size.asReadonly();
  readonly canDecrease = computed(() => this._size() > MIN_SIZE);
  readonly canIncrease = computed(() => this._size() < MAX_SIZE);
  readonly label = computed(() => {
    const map: Record<FontSize, string> = { 16: 'S', 18: 'M', 20: 'L', 22: 'XL', 24: 'XXL' };
    return map[this._size()];
  });

  constructor() {
    effect(() => {
      const px = this._size();
      document.documentElement.style.fontSize = `${px}px`;
      localStorage.setItem(STORAGE_KEY, String(px));
    });
  }

  increase() {
    const idx = SIZES.indexOf(this._size());
    if (idx < SIZES.length - 1) this._size.set(SIZES[idx + 1]);
  }

  decrease() {
    const idx = SIZES.indexOf(this._size());
    if (idx > 0) this._size.set(SIZES[idx - 1]);
  }

  private loadPersistedSize(): FontSize {
    const stored = localStorage.getItem(STORAGE_KEY);
    const parsed = stored ? Number(stored) : DEFAULT_SIZE;
    return (SIZES as readonly number[]).includes(parsed) ? (parsed as FontSize) : DEFAULT_SIZE;
  }
}
