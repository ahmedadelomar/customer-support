import { NgClass } from '@angular/common';
import { Component, ElementRef, computed, inject, signal, viewChild } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { TranslatePipe } from '@ngx-translate/core';
import { AuthService } from '../../core/auth/auth.service';
import { PERMISSIONS } from '../../core/permissions';
import { LanguageService } from '../../core/services/language.service';
import { NotificationBellComponent } from '../notifications/ui/notification-bell/notification-bell.component';
import { BranchSwitcherComponent } from './ui/branch-switcher/branch-switcher.component';
import { MentionsBadgeComponent } from './ui/mentions-badge/mentions-badge.component';
import { ReminderToastComponent } from './ui/reminder-toast/reminder-toast.component';

/** One entry in the side navigation. `permissions` gates visibility the same way the route does. */
interface NavItem {
  labelKey: string;
  icon: string;
  route: string;
  permissions?: string[];
}

interface NavGroup {
  labelKey: string;
  items: NavItem[];
}

/**
 * The agent workspace shell: side navigation, top bar and the routed outlet.
 *
 * Navigation is filtered by permission so an agent never sees a link that would be refused,
 * and the whole layout uses logical properties so it mirrors automatically in Arabic.
 */
@Component({
  selector: 'app-agent-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, NgClass, TranslatePipe, ReminderToastComponent, MentionsBadgeComponent, BranchSwitcherComponent, NotificationBellComponent],
  templateUrl: './agent-shell.page.html',
})
export class AgentShellPage {
  readonly #auth = inject(AuthService);
  readonly #language = inject(LanguageService);

  readonly user = this.#auth.user;
  readonly language = this.#language.current;

  /** Collapsed on small screens, toggled by the hamburger in the top bar. */
  readonly sidebarOpen = signal(false);

  readonly displayName = computed(() => {
    const user = this.user();
    if (!user) return '';
    return this.#language.pick({ en: user.displayNameEn, ar: user.displayNameAr });
  });

  /** Full navigation before permission filtering. Grouped to match the 12 product areas. */
  readonly #navigation: NavGroup[] = [
    {
      labelKey: 'nav.groups.work',
      items: [
        { labelKey: 'nav.dashboard', icon: '▦', route: '/agent/dashboard' },
        {
          labelKey: 'nav.tickets',
          icon: '✉',
          route: '/agent/tickets',
          permissions: [PERMISSIONS.tickets.view],
        },
        {
          labelKey: 'nav.customers',
          icon: '☺',
          route: '/agent/customers',
          permissions: [PERMISSIONS.customers.view],
        },
        {
          labelKey: 'nav.tasks',
          icon: '✓',
          route: '/agent/tasks',
          permissions: [PERMISSIONS.workspace.viewOwnTasks],
        },
        {
          labelKey: 'nav.mentions',
          icon: '@',
          route: '/agent/mentions',
          permissions: [PERMISSIONS.workspace.collaborate],
        },
      ],
    },
    {
      labelKey: 'nav.groups.knowledge',
      items: [
        {
          labelKey: 'nav.knowledgeBase',
          icon: '❐',
          route: '/agent/knowledge-base',
          permissions: [PERMISSIONS.knowledgeBase.view],
        },
        {
          labelKey: 'nav.quickReplies',
          icon: '⚡',
          route: '/agent/quick-replies',
          permissions: [PERMISSIONS.workspace.manageQuickReplies],
        },
      ],
    },
    {
      labelKey: 'nav.groups.insights',
      items: [
        {
          labelKey: 'nav.reports',
          icon: '◫',
          route: '/agent/reports',
          permissions: [PERMISSIONS.reports.viewTickets],
        },
      ],
    },
    {
      labelKey: 'nav.groups.administration',
      items: [
        {
          labelKey: 'nav.slaAutomation',
          icon: '◷',
          route: '/admin/sla',
          permissions: [PERMISSIONS.sla.view],
        },
        {
          labelKey: 'nav.channels',
          icon: '⇄',
          route: '/admin/channels',
          permissions: [PERMISSIONS.channels.view],
        },
        {
          labelKey: 'nav.users',
          icon: '⚿',
          route: '/admin/users',
          permissions: [PERMISSIONS.administration.viewUsers],
        },
        {
          labelKey: 'nav.settings',
          icon: '⚙',
          route: '/admin/settings',
          permissions: [PERMISSIONS.administration.manageSettings],
        },
      ],
    },
  ];

  /**
   * Navigation the current user may actually reach. Groups with no visible item are dropped,
   * so a limited role does not see empty headings.
   */
  readonly navigation = computed<NavGroup[]>(() => {
    // Depend on the user signal so the menu rebuilds on sign-in and role change.
    this.user();

    return this.#navigation
      .map((group) => ({
        ...group,
        items: group.items.filter(
          (item) => !item.permissions || this.#auth.hasAnyPermission(...item.permissions),
        ),
      }))
      .filter((group) => group.items.length > 0);
  });

  // `viewChild` cannot be declared on a native `#private` field.
  private readonly drawer = viewChild<ElementRef<HTMLElement>>('drawer');
  private readonly menuButton = viewChild<ElementRef<HTMLButtonElement>>('menuButton');

  toggleSidebar(): void {
    const opening = !this.sidebarOpen();
    this.sidebarOpen.set(opening);

    if (opening) {
      // Move focus into the drawer so a keyboard user lands where the visual focus went.
      queueMicrotask(() => this.#focusable()[0]?.focus());
    }
  }

  closeSidebar(): void {
    if (!this.sidebarOpen()) {
      return;
    }

    this.sidebarOpen.set(false);

    // Return focus to what opened it, rather than dropping the user at the top of the document.
    queueMicrotask(() => this.menuButton()?.nativeElement.focus());
  }

  /**
   * Keeps Tab inside the open drawer. Without this, tabbing walks into the page behind a drawer
   * that is visually covering it, which is disorienting with a screen reader and unusable with one.
   * Only active while the drawer is open; above `lg` it never opens, so the desktop layout is unaffected.
   */
  onDrawerKeydown(event: KeyboardEvent): void {
    if (!this.sidebarOpen()) {
      return;
    }

    if (event.key === 'Escape') {
      event.preventDefault();
      this.closeSidebar();
      return;
    }

    if (event.key !== 'Tab') {
      return;
    }

    const items = this.#focusable();
    if (items.length === 0) {
      return;
    }

    const first = items[0];
    const last = items[items.length - 1];
    const active = document.activeElement;

    if (event.shiftKey && active === first) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && active === last) {
      event.preventDefault();
      first.focus();
    }
  }

  #focusable(): HTMLElement[] {
    const root = this.drawer()?.nativeElement;
    if (!root) {
      return [];
    }

    return [
      ...root.querySelectorAll<HTMLElement>('a[href], button:not([disabled]), input, select, textarea'),
    ].filter((element) => element.offsetParent !== null);
  }

  toggleLanguage(): void {
    this.#language.toggle();
  }

  logout(): void {
    this.#auth.logout();
  }
}
