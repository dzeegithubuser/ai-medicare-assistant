import { PharmacyLookupEntry } from '../models/drug.model';

/** Collapse runs of the same character: "selecttt" → "select", "cvsss" → "cvs" */
export function collapseRepeatedChars(s: string): string {
  return s.replace(/(.)\1+/g, '$1');
}

function normalizeForMatch(s: string): string {
  return collapseRepeatedChars(s)
    .toLowerCase()
    .replace(/[^a-z0-9\s]/g, ' ')
    .replace(/\s+/g, ' ')
    .trim();
}

function levenshtein(a: string, b: string): number {
  if (a.length === 0) return b.length;
  if (b.length === 0) return a.length;
  const row: number[] = [];
  for (let j = 0; j <= b.length; j++) row[j] = j;
  for (let i = 1; i <= a.length; i++) {
    let prev = i - 1;
    row[0] = i;
    for (let j = 1; j <= b.length; j++) {
      const cur =
        a[i - 1] === b[j - 1]
          ? prev
          : 1 + Math.min(prev, row[j], row[j - 1]);
      prev = row[j];
      row[j] = cur;
    }
  }
  return row[b.length];
}

/** Split "a, b and c" → ["a","b","c"] */
export function splitPharmacyHints(fragment: string): string[] {
  return fragment
    .split(/\s*(?:,|;\s*|\s+and\s+)\s*/i)
    .map(s => s.trim())
    .filter(Boolean);
}

/** Normalize for exact comparison of pharmacy display names */
export function normalizePharmacyDisplayName(s: string): string {
  return s.toLowerCase().replace(/\s+/g, ' ').trim();
}

/**
 * 1-based index from phrases like "select 3rd", "the second", "pick #4", "select 3".
 * Uses the visible list order (same as lookup.pharmacies on this page).
 */
export function parseOrdinalIndex1Based(text: string): number | null {
  const t = collapseRepeatedChars(text).toLowerCase().trim();

  const ordinalWord: Record<string, number> = {
    first: 1,
    second: 2,
    third: 3,
    fourth: 4,
    fifth: 5,
    sixth: 6,
    seventh: 7,
    eighth: 8,
    ninth: 9,
    tenth: 10,
  };
  for (const [w, n] of Object.entries(ordinalWord)) {
    if (new RegExp(`\\b${w}\\b`).test(t)) return n;
  }

  const stNdRd = t.match(/\b(?:the\s+)?(\d{1,2})(?:st|nd|rd|th)\b/);
  if (stNdRd) {
    const n = parseInt(stNdRd[1], 10);
    return n >= 1 && n <= 99 ? n : null;
  }

  const hashNum = t.match(/\b(?:#|no\.?\s*|number\s*|pos(?:ition)?\s*)(\d{1,2})\b/);
  if (hashNum) {
    const n = parseInt(hashNum[1], 10);
    return n >= 1 && n <= 99 ? n : null;
  }

  const verbNum = t.match(/\b(?:select|pick|choose|add|remove|take)\s+(\d{1,2})\b/);
  if (verbNum) {
    const n = parseInt(verbNum[1], 10);
    if (n >= 1 && n <= 50) return n;
  }

  return null;
}

export type LocalPharmacyIntent =
  | { kind: 'select'; hints: string[] }
  | { kind: 'remove'; hints: string[] }
  | { kind: 'list' }
  | { kind: 'search'; term: string }
  | { kind: 'clearFilter' };

/**
 * Parse common chat phrases for pharmacy step. Returns null if nothing matched (use AI extract).
 */
export function parseLocalPharmacyIntent(raw: string): LocalPharmacyIntent | null {
  const collapsed = collapseRepeatedChars(raw).trim();
  const lower = collapsed.toLowerCase();

  if (
    /^(which|what|show|list)\b.*\b(pharmacies|pharmacy|selected|pick)/i.test(lower) ||
    /\bwhich\s+pharmacies\b/i.test(lower) ||
    /^what\s+(did\s+i\s+)?select/i.test(lower) ||
    /^show\s+(my\s+)?(pharmacies|selection)/i.test(lower)
  ) {
    return { kind: 'list' };
  }

  // Must run before generic "remove …" so "remove filter" does not try to remove a pharmacy named "filter"
  if (
    /^(?:remove|clear|reset)\s+(?:the\s+)?(?:search\s+)?filters?$/i.test(lower.trim()) ||
    /^clear\s+filters?$/i.test(lower.trim()) ||
    /^(?:show\s+)?all\s+(?:pharmacies|results)\s*$/i.test(lower.trim()) ||
    /^no\s+filters?$/i.test(lower.trim()) ||
    /^reset\s+(?:search|filters?)$/i.test(lower.trim())
  ) {
    return { kind: 'clearFilter' };
  }

  const searchMatch = lower.match(/^(?:search|find|filter)\s+(?:for\s+)?(.+)$/i);
  if (searchMatch?.[1]?.trim()) {
    return { kind: 'search', term: searchMatch[1].trim() };
  }

  const selectMatch = collapsed.match(/^(?:please\s+)?(?:select|add|pick|choose)\s+(.+)$/i);
  if (selectMatch?.[1]) {
    const hints = splitPharmacyHints(selectMatch[1]);
    if (hints.length) return { kind: 'select', hints };
  }

  const removeMatch = collapsed.match(/^(?:please\s+)?(?:remove|deselect|unselect|drop)\s+(.+)$/i);
  if (removeMatch?.[1]) {
    const hints = splitPharmacyHints(removeMatch[1]);
    if (hints.length) return { kind: 'remove', hints };
  }

  return null;
}

function scoreHintAgainstName(hintNorm: string, pharmacy: PharmacyLookupEntry): number {
  const name = String(pharmacy.pharmacyName ?? '');
  const nLower = name.toLowerCase();
  const nNorm = normalizeForMatch(name);

  if (!hintNorm.length) return 9999;

  if (nNorm.includes(hintNorm) || nLower.includes(hintNorm)) {
    return 0;
  }

  const hintTokens = hintNorm.split(/\s+/).filter(t => t.length >= 2);
  const nameTokens = nNorm.split(/\s+/).filter(t => t.length >= 2);
  let best = 9999;
  for (const ht of hintTokens) {
    for (const nt of nameTokens) {
      if (nt.includes(ht) || ht.includes(nt)) {
        return 1;
      }
      if (ht.length >= 3 && nt.length >= 3) {
        const d = levenshtein(ht, nt);
        const maxLen = Math.max(ht.length, nt.length);
        const threshold = Math.min(3, Math.floor(maxLen / 3) + 1);
        if (d <= threshold) {
          best = Math.min(best, d + 1);
        }
      }
    }
  }

  if (hintNorm.length >= 3 && hintNorm.length <= 20) {
    const slice = nNorm.slice(0, Math.min(nNorm.length, hintNorm.length + 12));
    const d = levenshtein(hintNorm, slice);
    if (d <= 4) {
      best = Math.min(best, d + 2);
    }
  }

  return best;
}

/**
 * Match one user hint to the best pharmacy (typo-tolerant). List order = distance order (prefer closer).
 */
export function matchPharmacyHint(hint: string, pharmacies: PharmacyLookupEntry[]): PharmacyLookupEntry | undefined {
  if (!pharmacies.length || !hint.trim()) return undefined;

  const hintNorm = normalizeForMatch(hint);
  let best: PharmacyLookupEntry | undefined;
  let bestScore = Infinity;

  for (const p of pharmacies) {
    const sc = scoreHintAgainstName(hintNorm, p);
    if (sc < bestScore) {
      bestScore = sc;
      best = p;
    }
  }

  if (bestScore <= 4 && best) {
    return best;
  }
  return undefined;
}

export interface ResolvedPharmacyPick {
  hint: string;
  pharmacy: PharmacyLookupEntry | null;
}

export function resolvePharmacyHints(
  hints: string[],
  pharmacies: PharmacyLookupEntry[],
): ResolvedPharmacyPick[] {
  const used = new Set<string>();
  const out: ResolvedPharmacyPick[] = [];
  for (const rawHint of hints) {
    const hint = rawHint.trim();
    if (!hint) continue;
    const candidates = pharmacies.filter(p => !used.has(String(p.pharmacyNumber)));
    const hintNorm = normalizePharmacyDisplayName(hint);
    const exact = candidates.find(p => normalizePharmacyDisplayName(p.pharmacyName) === hintNorm);
    let pharmacy = exact ?? matchPharmacyHint(hint, candidates);
    if (!pharmacy) {
      pharmacy = matchPharmacyHint(hint, pharmacies.filter(p => !used.has(String(p.pharmacyNumber))));
    }
    if (pharmacy) {
      used.add(String(pharmacy.pharmacyNumber));
    }
    out.push({ hint, pharmacy: pharmacy ?? null });
  }
  return out;
}
