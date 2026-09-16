import { NgClass } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { AuthService } from '../../core/auth/auth.service';
import { PERMISSIONS } from '../../core/permissions';
import { LanguageService } from '../../core/services/language.service';

interface NavItem {
  labelKey: string;
  icon: string;
  route: string;
  permissions?: string[];
}

/**
 * The admin/configuration shell: a lighter side navigation than the agent workspace, plus a link
 * back to the agent shell. First built for CS-202 (categories and priorities) — later admin areas
 * (SLA, channels, users, settings) add their own entries here rather than each growing their own shell.
 */
@Component({
  selector: 'app-admin-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, NgClass, TranslatePipe],
  templateUrl: './admin-shell.page.html',
})
export class AdminShellPage {
  readonly #auth = inject(AuthService);
  readonly #language = inject(LanguageService);

  readonly user = this.#auth.user;
  readonly language = this.#language.current;

  readonly sidebarOpen = signal(false);

  readonly displayName = computed(() => {
    const user = this.user();
    if (!user) return '';
    return this.#language.pick({ en: user.displayNameEn, ar: user.displayNameAr });
  });

  readonly #navigation: NavItem[] = [
    {
      labelKey: 'nav.ticketCategories',
      icon: '⊞',
      route: '/admin/ticket-categories',
      permissions: [PERMISSIONS.tickets.manageCategories],
    },
    {
      labelKey: 'nav.ticketPriorities',
      icon: '◆',
      route: '/admin/ticket-priorities',
      permissions: [PERMISSIONS.tickets.managePriorities],
    },
    {
      labelKey: 'nav.ticketStatuses',
      icon: '◷',
      route: '/admin/ticket-statuses',
      permissions: [PERMISSIONS.tickets.manageStatuses],
    },
    {
      labelKey: 'nav.users',
      icon: '⚿',
      route: '/admin/users',
      permissions: [PERMISSIONS.administration.viewUsers],
    },
    {
      labelKey: 'nav.roles',
      icon: '⚖',
      route: '/admin/roles',
      permissions: [PERMISSIONS.administration.manageRoles],
    },
  ];

  readonly navigation = computed<NavItem[]>(() => {
    this.user();
    return this.#navigation.filter(
      (item) => !item.permissions || this.#auth.hasAnyPermission(...item.permissions),
    );
  });

  toggleSidebar(): void {
    this.sidebarOpen.update((open) => !open);
  }

  closeSidebar(): void {
    this.sidebarOpen.set(false);
  }

  toggleLanguage(): void {
    this.#language.toggle();
  }

  logout(): void {
    this.#auth.logout();
  }
}
