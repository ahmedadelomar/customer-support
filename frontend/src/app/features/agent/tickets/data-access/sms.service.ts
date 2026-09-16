import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { Observable } from 'rxjs';
import { AppConfigService } from '../../../../core/services/app-config.service';

export interface SmsSegmentsResult {
  length: number;
  segmentCount: number;
  encoding: 'GSM-7' | 'UCS-2';
  maxLength: number;
  maxSegments: number;
}

/** Server-authoritative SMS segment calculation, used to fetch the configured cap (Communication Channels / SMS channel, CS-304). The live counter itself uses the local TS mirror for responsiveness. */
@Injectable({ providedIn: 'root' })
export class SmsService {
  readonly #http = inject(HttpClient);
  readonly #config = inject(AppConfigService);

  segments(text: string): Observable<SmsSegmentsResult> {
    return this.#http.post<SmsSegmentsResult>(`${this.#config.apiUrl}/api/channels/sms/segments`, { text });
  }
}
