import type { ContactType, CustomerType, ChannelKey, MessageDirection } from '../../../core/models/enums';
import type { PagedQuery } from '../../../core/models/api.models';

/** Mirrors `CustomerListItemDto`. */
export interface CustomerListItem {
  id: string;
  code: string;
  type: CustomerType;
  displayNameEn: string;
  displayNameAr: string;
  primaryEmail?: string;
  primaryPhone?: string;
  tier?: string;
  preferredLanguage: 'ar' | 'en';
  preferredChannel: ChannelKey;
  isActive: boolean;
  isBlocked: boolean;
  satisfactionScore?: number;
  lastInteractionAt?: string;
  openTicketCount: number;
  createdAt: string;
}

/** Mirrors `CustomerContactDto`. */
export interface CustomerContact {
  id: string;
  type: ContactType;
  value: string;
  label?: string;
  isPrimary: boolean;
  isVerified: boolean;
  allowNotifications: boolean;
  countryCode?: string;
  city?: string;
  addressLine?: string;
  postalCode?: string;
}

/** Mirrors `CustomerDetailDto`. */
export interface CustomerDetail {
  id: string;
  code: string;
  type: CustomerType;
  displayNameEn: string;
  displayNameAr: string;
  firstName?: string;
  lastName?: string;
  companyName?: string;
  nationalIdOrCr?: string;
  taxNumber?: string;
  primaryEmail?: string;
  primaryPhone?: string;
  preferredLanguage: 'ar' | 'en';
  preferredChannel: ChannelKey;
  timeZoneId?: string;
  tier?: string;
  accountManagerId?: string;
  branchId?: string;
  isActive: boolean;
  isBlocked: boolean;
  blockedReason?: string;
  satisfactionScore?: number;
  lastInteractionAt?: string;
  createdAt: string;
  modifiedAt?: string;
  contacts: CustomerContact[];
  totalTicketCount: number;
  openTicketCount: number;
  noteCount: number;
}

/** Query parameters accepted by `GET /api/customers`. */
export interface CustomerQuery extends PagedQuery {
  type?: CustomerType;
  tier?: string;
  isActive?: boolean;
  isBlocked?: boolean;
  accountManagerId?: string;
  hasOpenTickets?: boolean;
  createdFrom?: string;
  createdTo?: string;
}

/** Request body for create; matches `CreateCustomerCommand`. */
export interface CreateCustomerRequest {
  type: CustomerType;
  displayNameEn: string;
  displayNameAr: string;
  firstName?: string;
  lastName?: string;
  companyName?: string;
  nationalIdOrCr?: string;
  taxNumber?: string;
  email?: string;
  phone?: string;
  preferredLanguage: 'ar' | 'en';
  preferredChannel: ChannelKey;
  timeZoneId?: string;
  tier?: string;
  accountManagerId?: string;
  branchId?: string;
}

/** Request body for update; matches `UpdateCustomerCommand`. */
export interface UpdateCustomerRequest extends Omit<CreateCustomerRequest, 'email' | 'phone' | 'branchId'> {
  id: string;
  isActive: boolean;
  isBlocked: boolean;
  blockedReason?: string;
}

/** Request body for adding a contact; matches `AddCustomerContactCommand`. */
export interface AddContactRequest {
  type: ContactType;
  value: string;
  label?: string;
  countryCode?: string;
  city?: string;
  addressLine?: string;
  postalCode?: string;
  confirmDuplicate?: boolean;
}

/** Request body for editing a contact; matches `UpdateCustomerContactCommand`. The type is fixed. */
export interface UpdateContactRequest extends Omit<AddContactRequest, 'type'> {
  allowNotifications: boolean;
}

/**
 * Extra fields `errorInterceptor`/the contact dialog read off a 409 problem-details response for
 * `DuplicateContactException`, alongside the standard `ApiProblem` fields.
 */
export interface DuplicateContactProblem {
  duplicateCustomerId: string;
  duplicateCustomerName: string;
}

/** Mirrors `SendContactVerificationResultDto`. */
export interface SendVerificationResult {
  expiresAt: string;
  /** Set only while no real email/SMS sender is wired up (CS-301/CS-304). */
  devCode?: string;
}

/** Mirrors `ConfirmContactVerificationResultDto`. */
export interface ConfirmVerificationResult {
  success: boolean;
  remainingAttempts: number;
}

/** Mirrors `InteractionDto`. */
export interface Interaction {
  id: string;
  ticketId?: string;
  ticketNumber?: string;
  channel: ChannelKey;
  direction: MessageDirection;
  subject?: string;
  preview?: string;
  agentId?: string;
  agentNameEn?: string;
  agentNameAr?: string;
  occurredAt: string;
}

/** Mirrors `InteractionPageDto`. Not `PagedResult<T>` — this is keyset-paged, with no total count. */
export interface InteractionPage {
  items: Interaction[];
  hasMore: boolean;
}

/** Query parameters accepted by `GET /api/customers/{id}/interactions`. */
export interface InteractionQuery {
  before?: string;
  beforeId?: string;
  channel?: ChannelKey;
  direction?: MessageDirection;
  from?: string;
  to?: string;
  pageSize?: number;
}
