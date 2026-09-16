import { calculateSmsSegments } from './sms-segment-calculator';
import fixture from '../../../../../test-fixtures/sms-segment-vectors.json';

interface SmsSegmentVector {
  name: string;
  text?: string;
  repeatChar?: string;
  repeatCharTimes?: number;
  suffix?: string;
  length: number;
  segments: number;
  encoding: 'GSM-7' | 'UCS-2';
}

function buildText(vector: SmsSegmentVector): string {
  if (vector.text !== undefined) return vector.text;
  return vector.repeatChar!.repeat(vector.repeatCharTimes!) + (vector.suffix ?? '');
}

describe('calculateSmsSegments', () => {
  // Same test-fixtures/sms-segment-vectors.json the C# SmsSegmentCalculatorTests reads — the two
  // implementations must never quietly disagree about what an agent is billed for.
  for (const vector of fixture.vectors as SmsSegmentVector[]) {
    it(`matches the shared fixture: ${vector.name}`, () => {
      const result = calculateSmsSegments(buildText(vector));

      expect(result.length).toBe(vector.length);
      expect(result.segmentCount).toBe(vector.segments);
      expect(result.encoding).toBe(vector.encoding);
    });
  }
});
