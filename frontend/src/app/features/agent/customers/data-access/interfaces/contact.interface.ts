import type { ContactType } from '../../../../../core/models/enums';

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
