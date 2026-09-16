/**
 * Fails the build when en.json and ar.json do not have identical key sets.
 *
 * A key present in only one file renders as the raw key in the other language — silently, and only
 * for users of that language, which is why it is the single most common i18n bug in this codebase's
 * class of app. Making it a build failure is cheaper than finding it in production.
 */
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

const here = dirname(fileURLToPath(import.meta.url));
const i18nDir = join(here, '..', 'public', 'i18n');

/** Flattens a nested translation object to dotted key paths. */
function flatten(value, prefix = '') {
  if (value === null || typeof value !== 'object' || Array.isArray(value)) {
    return [prefix];
  }

  return Object.entries(value).flatMap(([key, child]) =>
    flatten(child, prefix ? `${prefix}.${key}` : key),
  );
}

function load(language) {
  const path = join(i18nDir, `${language}.json`);

  try {
    return new Set(flatten(JSON.parse(readFileSync(path, 'utf8'))));
  } catch (error) {
    console.error(`✘ Could not read ${path}: ${error.message}`);
    process.exit(1);
  }
}

const en = load('en');
const ar = load('ar');

const missingInAr = [...en].filter((key) => !ar.has(key)).sort();
const missingInEn = [...ar].filter((key) => !en.has(key)).sort();

if (missingInAr.length === 0 && missingInEn.length === 0) {
  console.log(`✔ i18n parity: ${en.size} keys in both en.json and ar.json.`);
  process.exit(0);
}

if (missingInAr.length > 0) {
  console.error(`✘ ${missingInAr.length} key(s) missing from ar.json:`);
  for (const key of missingInAr) console.error(`    ${key}`);
}

if (missingInEn.length > 0) {
  console.error(`✘ ${missingInEn.length} key(s) missing from en.json:`);
  for (const key of missingInEn) console.error(`    ${key}`);
}

process.exit(1);
