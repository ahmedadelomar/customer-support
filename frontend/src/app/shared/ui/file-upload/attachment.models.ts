/** Mirrors `AttachmentDto`. */
export interface Attachment {
  id: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  createdAt: string;
}

/** Mirrors `AttachmentPolicy` — read once by `FileUploadComponent` to validate before uploading. */
export interface AttachmentPolicy {
  maxBytes: number;
  allowedExtensions: string[];
}

/** One file's local upload state, tracked in the file-upload component. */
export interface UploadItem {
  id: string;
  file: File;
  progress: number;
  status: 'pending' | 'uploading' | 'done' | 'error';
  attachment?: Attachment;
  errorMessage?: string;
}
