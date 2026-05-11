import { test, expect } from '@playwright/test';
import type { AuditEventView, AuditPage, SeedRequest } from './types';

// Each test seeds its own records under a unique source so cases don't bleed into each other.
function uniqueSource(): string {
  return `pwtest-${crypto.randomUUID().replace(/-/g, '').slice(0, 12)}`;
}

async function seed(request: import('@playwright/test').APIRequestContext, body: SeedRequest): Promise<AuditEventView> {
  const response = await request.post('/api/test/audits', { data: body });
  expect(response.status(), `seed failed: ${await response.text()}`).toBe(201);
  return await response.json();
}

test.afterAll(async ({ request }) => {
  // Best-effort cleanup so the local Mongo doesn't grow unbounded.
  // The seed endpoint's DELETE wipes the entire collection; call it once at the end.
  // No assertion — if Mongo is already empty (e.g. fresh container) the call is a no-op.
  await request.delete('/api/test/audits');
});

test('Search_NoFilters_ReturnsRecentItemsPaged', async ({ request }) => {
  const source = uniqueSource();
  await seed(request, { source, entityType: 'FillUp', entityId: '{"Id":1}', action: 'Insert' });
  await seed(request, { source, entityType: 'FillUp', entityId: '{"Id":2}', action: 'Insert' });

  const response = await request.get('/api/audits', { params: { source, limit: 10 } });

  expect(response.status()).toBe(200);
  const page: AuditPage = await response.json();
  expect(page.total).toBeGreaterThanOrEqual(2);
  expect(page.items.length).toBeGreaterThanOrEqual(2);
  expect(page.items.every(i => i.source === source)).toBe(true);
});

test('Search_FilterByEntityType_ReturnsOnlyMatching', async ({ request }) => {
  const source = uniqueSource();
  await seed(request, { source, entityType: 'FillUp', entityId: '{"Id":1}', action: 'Insert' });
  await seed(request, { source, entityType: 'Vehicle', entityId: '{"Id":1}', action: 'Insert' });

  const response = await request.get('/api/audits', {
    params: { source, entityType: 'FillUp', limit: 10 },
  });

  expect(response.status()).toBe(200);
  const page: AuditPage = await response.json();
  expect(page.items.every(i => i.entityType === 'FillUp')).toBe(true);
});

test('Search_FilterByUser_ReturnsOnlyThatUser', async ({ request }) => {
  const source = uniqueSource();
  await seed(request, { source, entityType: 'X', entityId: '1', userId: 'alice', action: 'Insert' });
  await seed(request, { source, entityType: 'X', entityId: '2', userId: 'bob', action: 'Insert' });

  const response = await request.get('/api/audits', {
    params: { source, userId: 'alice', limit: 10 },
  });

  const page: AuditPage = await response.json();
  expect(page.items.every(i => i.userId === 'alice')).toBe(true);
  expect(page.items.length).toBeGreaterThanOrEqual(1);
});

test('Search_OrdersByOccurredAtDescending', async ({ request }) => {
  const source = uniqueSource();
  const earlier = '2024-01-01T00:00:00Z';
  const later = '2025-06-01T00:00:00Z';
  await seed(request, { source, entityType: 'X', entityId: '1', action: 'Insert', occurredAt: earlier });
  await seed(request, { source, entityType: 'X', entityId: '2', action: 'Insert', occurredAt: later });

  const response = await request.get('/api/audits', { params: { source, limit: 10 } });

  const page: AuditPage = await response.json();
  // Compare as instants — the API serializes DateTimeOffset as `...+00:00` while
  // we sent `...Z`, so a string compare would spuriously fail.
  expect(new Date(page.items[0].occurredAt).getTime()).toBe(new Date(later).getTime());
  expect(new Date(page.items[1].occurredAt).getTime()).toBe(new Date(earlier).getTime());
});

test('GetById_Found_ReturnsRecord', async ({ request }) => {
  const created = await seed(request, {
    source: uniqueSource(),
    entityType: 'FillUp',
    entityId: '{"Id":42}',
    action: 'Insert',
  });

  const response = await request.get(`/api/audits/${created.id}`);

  expect(response.status()).toBe(200);
  const view: AuditEventView = await response.json();
  expect(view.id).toBe(created.id);
  expect(view.entityType).toBe('FillUp');
});

test('GetById_NotFound_Returns404', async ({ request }) => {
  // Valid 24-char hex ObjectId that won't exist (all-f).
  const response = await request.get('/api/audits/ffffffffffffffffffffffff');

  expect(response.status()).toBe(404);
});

test('GetByEntity_ReturnsAllRecordsForEntityNewestFirst', async ({ request }) => {
  const source = uniqueSource();
  await seed(request, {
    source, entityType: 'FillUp', entityId: '{"Id":99}', action: 'Insert',
    occurredAt: '2024-01-01T00:00:00Z',
  });
  await seed(request, {
    source, entityType: 'FillUp', entityId: '{"Id":99}', action: 'Update',
    occurredAt: '2024-06-01T00:00:00Z',
  });

  const response = await request.get('/api/audits/by-entity/FillUp/{"Id":99}');

  expect(response.status()).toBe(200);
  const items: AuditEventView[] = await response.json();
  // At least the two we just seeded; might match more if the source happens to match.
  const ours = items.filter(i => i.source === source);
  expect(ours).toHaveLength(2);
  expect(ours[0].action).toBe('Update'); // newer
  expect(ours[1].action).toBe('Insert'); // older
});

test('GetSources_IncludesSeededSource', async ({ request }) => {
  const source = uniqueSource();
  await seed(request, { source, entityType: 'X', entityId: '1', action: 'Insert' });

  const response = await request.get('/api/audits/sources');

  expect(response.status()).toBe(200);
  const sources: string[] = await response.json();
  expect(sources).toContain(source);
});

test('Search_RespectsSkipAndLimit', async ({ request }) => {
  const source = uniqueSource();
  for (let i = 0; i < 5; i++) {
    await seed(request, { source, entityType: 'X', entityId: String(i), action: 'Insert' });
  }

  const firstPage = await (await request.get('/api/audits', {
    params: { source, skip: 0, limit: 2 },
  })).json() as AuditPage;
  const secondPage = await (await request.get('/api/audits', {
    params: { source, skip: 2, limit: 2 },
  })).json() as AuditPage;

  expect(firstPage.items).toHaveLength(2);
  expect(secondPage.items).toHaveLength(2);
  // Pages don't overlap.
  const ids = new Set([...firstPage.items, ...secondPage.items].map(i => i.id));
  expect(ids.size).toBe(4);
});

test('Search_NoToken_Returns401', async ({ playwright }) => {
  // Fresh request context with explicit empty extraHTTPHeaders to override the
  // project-default Authorization header — otherwise the bearer token would
  // still be attached and we'd never exercise the unauthenticated path.
  const anon = await playwright.request.newContext({
    baseURL: process.env.AUDIT_BASE_URL ?? 'http://localhost:5296',
    extraHTTPHeaders: {},
  });
  try {
    const response = await anon.get('/api/audits');
    expect(response.status()).toBe(401);
  } finally {
    await anon.dispose();
  }
});
