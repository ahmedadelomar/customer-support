import { Injectable, signal } from '@angular/core';

export interface Toast {
  id: number;
  severity: 'success' | 'error' | 'info' | 'warning';
  /** Either a translation key or a ready-made message; the component tries translation first. */
  message: string;
  timeoutMs: number;
}

/** Transient notifications. The shell renders `toasts()`; anything can push to it. */
@Injectable({ providedIn: 'root' })
export class ToastService {
  readonly #toasts = signal<Toast[]>([]);
  readonly toasts = this.#toasts.asReadonly();

  #nextId = 1;

  success(message: string, timeoutMs = 4000): void {
    this.#push('success', message, timeoutMs);
  }

  error(message: string, timeoutMs = 7000): void {
    this.#push('error', message, timeoutMs);
  }

  info(message: string, timeoutMs = 4000): void {
    this.#push('info', message, timeoutMs);
  }

  warning(message: string, timeoutMs = 5000): void {
    this.#push('warning', message, timeoutMs);
  }

  dismiss(id: number): void {
    this.#toasts.update((list) => list.filter((t) => t.id !== id));
  }

  #push(severity: Toast['severity'], message: string, timeoutMs: number): void {
    const toast: Toast = { id: this.#nextId++, severity, message, timeoutMs };
    this.#toasts.update((list) => [...list, toast]);

    if (timeoutMs > 0) {
      setTimeout(() => this.dismiss(toast.id), timeoutMs);
    }
  }
}
