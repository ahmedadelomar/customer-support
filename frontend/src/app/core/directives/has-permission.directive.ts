import {
  Directive,
  TemplateRef,
  ViewContainerRef,
  effect,
  inject,
  input,
} from '@angular/core';
import { AuthService } from '../auth/auth.service';

/**
 * Structural directive that renders content only when the user holds the permission(s).
 *
 * ```html
 * <button *hasPermission="PERMISSIONS.customers.create">New customer</button>
 * <button *hasPermission="[PERMISSIONS.tickets.assign]; mode: 'any'">Assign</button>
 * ```
 *
 * Hiding a control is a usability affordance, not a security boundary — the server enforces
 * the same permission on every request.
 */
@Directive({ selector: '[hasPermission]' })
export class HasPermissionDirective {
  readonly #auth = inject(AuthService);
  readonly #template = inject(TemplateRef<unknown>);
  readonly #container = inject(ViewContainerRef);

  readonly hasPermission = input.required<string | string[]>();

  /** `all` (default) requires every key; `any` requires at least one. */
  readonly hasPermissionMode = input<'all' | 'any'>('all');

  #rendered = false;

  constructor() {
    effect(() => {
      const keys = ([] as string[]).concat(this.hasPermission());
      const allowed =
        this.hasPermissionMode() === 'any'
          ? this.#auth.hasAnyPermission(...keys)
          : this.#auth.hasPermission(...keys);

      if (allowed && !this.#rendered) {
        this.#container.createEmbeddedView(this.#template);
        this.#rendered = true;
      } else if (!allowed && this.#rendered) {
        this.#container.clear();
        this.#rendered = false;
      }
    });
  }
}
