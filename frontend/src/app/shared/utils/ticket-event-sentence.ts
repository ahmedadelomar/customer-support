import { TicketEventType } from '../../core/models/enums';
import type { TicketEvent } from '../../features/agent/tickets/data-access/interfaces/ticket-event.interface';

/** A translation key plus its interpolation params, ready for `| translate: params`. */
export interface EventSentence {
  key: string;
  params: Record<string, string | number>;
}

/** Icon shown next to each event type on the History tab and inline in the conversation. */
const EVENT_ICONS: Record<TicketEventType, string> = {
  [TicketEventType.Created]: '✚',
  [TicketEventType.StatusChanged]: '◷',
  [TicketEventType.PriorityChanged]: '◆',
  [TicketEventType.CategoryChanged]: '⊞',
  [TicketEventType.Assigned]: '👤',
  [TicketEventType.Unassigned]: '👤',
  [TicketEventType.Escalated]: '⚠',
  [TicketEventType.MessageAdded]: '💬',
  [TicketEventType.InternalNoteAdded]: '🔒',
  [TicketEventType.AttachmentAdded]: '📎',
  [TicketEventType.SlaBreached]: '⏱',
  [TicketEventType.Merged]: '⇄',
  [TicketEventType.Reopened]: '↺',
  [TicketEventType.Resolved]: '✔',
  [TicketEventType.Closed]: '⏹',
  [TicketEventType.DepartmentChanged]: '🏢',
  [TicketEventType.TagsChanged]: '🏷',
  [TicketEventType.WatcherAdded]: '👁',
  [TicketEventType.FollowUpCreated]: '↪',
  [TicketEventType.WatcherRemoved]: '👁',
};

export function ticketEventIcon(eventType: TicketEventType): string {
  return EVENT_ICONS[eventType] ?? '•';
}

/**
 * Maps a ticket event to a parameterised translation key — never string concatenation, so Arabic
 * word order comes from the translation file rather than fixed English sentence structure. Always
 * reads `oldDisplayValue`/`newDisplayValue` off the event itself, never a fresh lookup, so a later
 * rename of a status/category/priority never rewrites past history (see the story's product rules).
 *
 * `systemLabel` is the already-translated word for "System" (e.g. `translate.instant('tickets.history.system')`),
 * substituted for the actor only when the event is automation-generated and names no rule.
 */
export function ticketEventSentence(event: TicketEvent, systemLabel: string): EventSentence {
  const actor = event.isSystemGenerated ? (event.triggeredByRule ?? systemLabel) : (event.actorDisplayName ?? systemLabel);
  const from = event.oldDisplayValue ?? '';
  const to = event.newDisplayValue ?? '';

  const base = (suffix: number, params: Record<string, string | number> = {}) => ({
    key: `tickets.history.events.${suffix}`,
    params: { actor, ...params },
  });

  switch (event.eventType) {
    case TicketEventType.Created:
      return base(0);
    case TicketEventType.StatusChanged:
      return base(1, { from, to });
    case TicketEventType.PriorityChanged:
      return base(2, { from, to });
    case TicketEventType.CategoryChanged:
      return base(3, { from, to });
    case TicketEventType.Assigned:
      return base(4, { to });
    case TicketEventType.Unassigned:
      return base(5, { from });
    case TicketEventType.Escalated:
      return base(6, { level: event.newValue ?? '' });
    case TicketEventType.MessageAdded:
      return base(7);
    case TicketEventType.InternalNoteAdded:
      return base(8);
    case TicketEventType.AttachmentAdded:
      return base(9);
    case TicketEventType.SlaBreached:
      return { key: 'tickets.history.events.10', params: { target: to || from } };
    case TicketEventType.Merged:
      return base(11, { to });
    case TicketEventType.Reopened:
      return base(12);
    case TicketEventType.Resolved:
      return base(13);
    case TicketEventType.Closed:
      return base(14);
    case TicketEventType.DepartmentChanged:
      return base(15, { from, to });
    case TicketEventType.TagsChanged:
      return base(16);
    case TicketEventType.WatcherAdded:
      return base(17);
    case TicketEventType.FollowUpCreated:
      return base(18);
    case TicketEventType.WatcherRemoved:
      return base(19);
    default:
      return base(0);
  }
}
