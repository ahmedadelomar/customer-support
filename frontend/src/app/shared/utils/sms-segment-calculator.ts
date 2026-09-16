/** Mirrors the backend's `SmsSegments` record (Communication Channels / SMS channel, CS-304). */
export interface SmsSegments {
  length: number;
  segmentCount: number;
  encoding: 'GSM-7' | 'UCS-2';
  maxLength: number;
}

/** The GSM 03.38 default alphabet's basic character set — one septet each. */
const GSM_BASIC =
  "@£$¥èéùìòÇ\nØø\rÅåΔ_ΦΓΛΩΠΨΣΘΞ !\"#¤%&'()*+,-./0123456789:;<=>?¡ABCDEFGHIJKLMNOPQRSTUVWXYZÄÖÑÜ§¿abcdefghijklmnopqrstuvwxyzäöñüà";

/** Extension table characters — each costs two septets (an escape sequence plus the character). */
const GSM_EXTENDED = '^{}\\[~]|€';

const GSM_CHARSET = new Set((GSM_BASIC + GSM_EXTENDED).split(''));
const GSM_EXTENDED_CHARSET = new Set(GSM_EXTENDED.split(''));

/**
 * GSM-7 vs UCS-2 segment math, mirrored exactly from the C# `SmsSegmentCalculator` against the same
 * `test-fixtures/sms-segment-vectors.json` — this is the client-side live counter only; the server
 * (`POST /api/channels/sms/segments`) is authoritative before any send is actually attempted.
 */
export function calculateSmsSegments(text: string): SmsSegments {
  const chars = Array.from(text ?? '');

  // A single character outside the GSM-7 alphabet forces the whole message to UCS-2 — there is no
  // mixed encoding on the wire.
  const isGsm = chars.every((c) => GSM_CHARSET.has(c));

  const perSegment = isGsm ? 160 : 70;
  const perConcatenated = isGsm ? 153 : 67;
  const length = isGsm ? chars.reduce((sum, c) => sum + (GSM_EXTENDED_CHARSET.has(c) ? 2 : 1), 0) : chars.length;

  const segmentCount = length <= perSegment ? 1 : Math.ceil(length / perConcatenated);

  return { length, segmentCount, encoding: isGsm ? 'GSM-7' : 'UCS-2', maxLength: perSegment };
}
