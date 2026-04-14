import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join, extname } from 'node:path';

const ROOT = process.cwd();
const SRC_DIR = join(ROOT, 'src');
const MAX_LINES = 700;
const ALLOWED_EXT = new Set(['.ts', '.html', '.scss']);
const IGNORE_DIRS = new Set(['node_modules', '.angular', 'dist', 'coverage']);
const LEGACY_ALLOWLIST = new Set([]);

/** @type {{ path: string; lines: number }[]} */
const violations = [];

function walk(dir) {
  const entries = readdirSync(dir);
  for (const entry of entries) {
    if (IGNORE_DIRS.has(entry)) continue;
    const full = join(dir, entry);
    const st = statSync(full);
    if (st.isDirectory()) {
      walk(full);
      continue;
    }
    const ext = extname(full);
    if (!ALLOWED_EXT.has(ext)) continue;
    const content = readFileSync(full, 'utf8');
    const lines = content.split(/\r?\n/).length;
    const relative = full.replace(ROOT + '\\', '');
    if (lines > MAX_LINES && !LEGACY_ALLOWLIST.has(relative)) {
      violations.push({ path: relative, lines });
    }
  }
}

walk(SRC_DIR);

if (violations.length === 0) {
  console.log(`OK: no non-legacy files exceed ${MAX_LINES} lines in src/`);
  process.exit(0);
}

console.error(`Found ${violations.length} file(s) above ${MAX_LINES} lines:`);
for (const v of violations.sort((a, b) => b.lines - a.lines)) {
  console.error(` - ${v.path} (${v.lines})`);
}
process.exit(1);
