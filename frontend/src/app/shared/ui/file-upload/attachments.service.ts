import { HttpClient, HttpEventType, type HttpEvent } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { AppConfigService } from '../../../core/services/app-config.service';
import type { Attachment, AttachmentPolicy } from './attachment.models';

/**
 * Data access for the shared attachment store. Lives under `shared/ui/file-upload/` alongside the
 * component that is its only intended caller today — any future feature (ticket replies, KB
 * authoring) reusing the upload UI gets this service for free by importing the same component.
 */
@Injectable({ providedIn: 'root' })
export class AttachmentsService {
  readonly #http = inject(HttpClient);
  readonly #config = inject(AppConfigService);

  readonly #baseUrl = `${this.#config.apiUrl}/api/attachments`;

  /** Read once by the upload component so oversized or disallowed files are rejected before any network call. */
  getPolicy(): Observable<AttachmentPolicy> {
    return this.#http.get<AttachmentPolicy>(`${this.#baseUrl}/policy`);
  }

  /** Emits `HttpEvent`s so the caller can surface per-file upload progress. */
  upload(ownerType: string, ownerId: string, file: File): Observable<HttpEvent<Attachment>> {
    const form = new FormData();
    form.append('ownerType', ownerType);
    form.append('ownerId', ownerId);
    form.append('file', file, file.name);

    return this.#http.post<Attachment>(this.#baseUrl, form, {
      reportProgress: true,
      observe: 'events',
    });
  }

  delete(attachmentId: string): Observable<void> {
    return this.#http.delete<void>(`${this.#baseUrl}/${attachmentId}`);
  }

  /** The download URL. Actual retrieval goes through an authenticated blob request — see `downloadBlob`. */
  downloadUrl(attachmentId: string): string {
    return `${this.#baseUrl}/${attachmentId}`;
  }

  /**
   * Downloads as a blob rather than a plain anchor href: the endpoint requires a bearer token,
   * which only a real HTTP request (not a navigated link) can carry.
   */
  downloadBlob(attachmentId: string): Observable<Blob> {
    return this.#http.get(this.downloadUrl(attachmentId), { responseType: 'blob' });
  }

  static isUploadProgress(event: HttpEvent<unknown>): event is HttpEvent<unknown> & { loaded: number; total?: number } {
    return event.type === HttpEventType.UploadProgress;
  }
}
