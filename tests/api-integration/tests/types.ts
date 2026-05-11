// Wire shapes mirroring Spydersoft.Audit.Client DTOs. Kept narrow — only the
// fields the test bodies actually read or send.

export interface AuditEventView {
  id: string;
  source: string;
  eventType: string;
  entityType: string;
  entityId: string | null;
  action: string;
  userId: string | null;
  occurredAt: string; // ISO 8601
  oldValues: string | null;
  newValues: string | null;
  correlationId: string | null;
  messageId: string | null;
}

export interface AuditPage {
  items: AuditEventView[];
  total: number;
  skip: number;
  limit: number;
}

export interface SeedRequest {
  source: string;
  entityType: string;
  entityId?: string | null;
  userId?: string | null;
  eventType?: string | null;
  action?: string | null;
  occurredAt?: string | null;
  oldValues?: string | null;
  newValues?: string | null;
  correlationId?: string | null;
  messageId?: string | null;
}
