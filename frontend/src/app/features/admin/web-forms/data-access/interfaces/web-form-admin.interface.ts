import type { WebFormField } from '../../../../public/web-forms/data-access/interfaces/web-form.interface';

export interface WebFormDefinition {
  id: string;
  key: string;
  titleEn: string;
  titleAr: string;
  descriptionEn: string;
  descriptionAr: string;
  submitButtonLabelEn: string;
  submitButtonLabelAr: string;
  thankYouMessageEn: string;
  thankYouMessageAr: string;
  fields: WebFormField[];
  defaultCategoryId: string | null;
  defaultPriorityId: string | null;
  defaultDepartmentId: string | null;
  requireCaptcha: boolean;
  rateLimitPerHour: number;
  isActive: boolean;
  submissionCount: number;
}

export type WebFormDefinitionRequest = Omit<WebFormDefinition, 'id' | 'submissionCount'>;

export interface WebFormSubmission {
  id: string;
  webFormDefinitionId: string;
  submitterName: string | null;
  submitterEmail: string | null;
  submitterPhone: string | null;
  status: string;
  failureReason: string | null;
  ticketId: string | null;
  ticketNumber: string | null;
  submittedAt: string;
  processedAt: string | null;
  payloadJson: string;
}

export type { WebFormField, WebFormFieldOption, WebFormFieldType } from '../../../../public/web-forms/data-access/interfaces/web-form.interface';
