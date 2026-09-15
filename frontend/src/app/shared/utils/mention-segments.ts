/** One piece of a note body, split around structured `@[Name](userId)` mention markers. */
export type MentionSegment = { kind: 'text'; text: string } | { kind: 'mention'; userId: string; name: string };

const MENTION_PATTERN = /@\[([^\]]+)\]\(([0-9a-fA-F-]{36})\)/g;

/**
 * Splits a note body around its structured mention markers so the template can render each mention
 * as a distinct chip via `@for`, rather than through `innerHTML` — there is no HTML to sanitize this
 * way, and a stray `@[` in ordinary text (no matching close) is just left as plain text.
 */
export function parseMentionSegments(bodyText: string): MentionSegment[] {
  const segments: MentionSegment[] = [];
  let lastIndex = 0;

  for (const match of bodyText.matchAll(MENTION_PATTERN)) {
    const index = match.index ?? 0;
    if (index > lastIndex) {
      segments.push({ kind: 'text', text: bodyText.slice(lastIndex, index) });
    }
    segments.push({ kind: 'mention', userId: match[2], name: match[1] });
    lastIndex = index + match[0].length;
  }

  if (lastIndex < bodyText.length) {
    segments.push({ kind: 'text', text: bodyText.slice(lastIndex) });
  }

  return segments;
}
