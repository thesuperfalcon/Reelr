import { vi } from "vitest";

// A JWT with the claims AuthProvider reads. The signature is never checked in the browser.
export function fakeToken(id: number, username: string, expiresInSeconds = 900): string {
  const encode = (value: object) => btoa(JSON.stringify(value)).replace(/=+$/, "").replace(/\+/g, "-").replace(/\//g, "_");
  return `${encode({ alg: "HS256" })}.${encode({ sub: String(id), unique_name: username, exp: Math.floor(Date.now() / 1000) + expiresInSeconds })}.signature`;
}

export function json(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), { status, headers: { "Content-Type": "application/json" } });
}

type Route = (request: Request) => Response | Promise<Response>;

// Replaces fetch with routes keyed by "METHOD /path". Unknown requests answer 404. Returns the mock, so tests
// can look at what was sent.
export function mockFetch(routes: Record<string, Route>) {
  const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const request = new Request(new URL(String(input), "http://localhost"), init);
    const route = routes[`${request.method} ${new URL(request.url).pathname}`];
    return route ? route(request) : new Response(null, { status: 404 });
  });
  vi.stubGlobal("fetch", fetchMock);
  return fetchMock;
}

// The Request each call was made with.
export function requestsOf(fetchMock: ReturnType<typeof mockFetch>): Request[] {
  return fetchMock.mock.calls.map(([input, init]) => new Request(new URL(String(input), "http://localhost"), init));
}
