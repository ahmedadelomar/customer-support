export interface WebFormFieldOption {
  value: string;
  labelEn: string;
  labelAr: string;
}

/** Field types the schema-driven renderer and the admin builder both understand. */
export type WebFormFieldType = 'text' | 'email' | 'phone' | 'number' | 'date' | 'select' | 'multiselect' | 'textarea' | 'checkbox';

export interface WebFormField {
  key: string;
  type: WebFormFieldType;
  labelEn: string;
  labelAr: string;
  placeholderEn: string | null;
  placeholderAr: string | null;
  required: boolean;
  maxLength: number | null;
  pattern: string | null;
  options: WebFormFieldOption[] | null;
  /** customerEmail | customerPhone | customerName | subject | description | categoryCode | priorityCode | null (unmapped). */
  mapTo: string | null;
}

export interface PublicWebFormSchema {
  key: string;
  titleEn: string;
  titleAr: string;
  descriptionEn: string;
  descriptionAr: string;
  submitButtonLabelEn: string;
  submitButtonLabelAr: string;
  fields: WebFormField[];
  requireCaptcha: boolean;
}

export interface SubmitWebFormResult {
  submissionId: string;
  ticketId: string | null;
  ticketNumber: string | null;
  thankYouMessageEn: string;
  thankYouMessageAr: string;
}
