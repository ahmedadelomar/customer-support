import { Injectable } from '@angular/core';
import { environment } from '../../../environments/environment';

/**
 * Single source for runtime configuration. Services read the API base URL from here rather than
 * importing `environment` directly, so a runtime-config strategy can replace it in one place.
 */
@Injectable({ providedIn: 'root' })
export class AppConfigService {
  get apiUrl(): string {
    return environment.apiUrl;
  }

  get production(): boolean {
    return environment.production;
  }

  /** Page size used by every list screen unless the user changes it. */
  get defaultPageSize(): number {
    return 20;
  }
}
