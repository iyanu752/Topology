const DEFAULT_API_BASE_URL = "http://localhost:5023";

export type ApiRequestOptions = Omit<RequestInit, "body"> & {
  accessToken?: string;
  body?: unknown;
};

export class ApiError extends Error {
  status: number;
  details: unknown;

  constructor(message: string, status: number, details: unknown = null) {
    super(message);
    this.name = "ApiError";
    this.status = status;
    this.details = details;
  }
}

export function getApiBaseUrl() {
  return process.env.NEXT_PUBLIC_API_BASE_URL ?? DEFAULT_API_BASE_URL;
}

export async function apiRequest<T>(path: string, options: ApiRequestOptions = {}): Promise<T> {
  const { accessToken, body, headers, ...requestOptions } = options;
  const requestHeaders = new Headers(headers);

  if (body !== undefined && !requestHeaders.has("Content-Type")) {
    requestHeaders.set("Content-Type", "application/json");
  }

  if (!requestHeaders.has("Accept")) {
    requestHeaders.set("Accept", "application/json");
  }

  if (accessToken) {
    requestHeaders.set("Authorization", `Bearer ${accessToken}`);
  }

  const response = await fetch(`${getApiBaseUrl()}${path}`, {
    ...requestOptions,
    headers: requestHeaders,
    body: body === undefined ? undefined : JSON.stringify(body)
  });

  if (response.status === 204) {
    return undefined as T;
  }

  const responseText = await response.text();
  const parsedBody = parseResponseBody(responseText);

  if (!response.ok) {
    throw new ApiError(getErrorMessage(parsedBody, response.statusText), response.status, parsedBody);
  }

  return parsedBody as T;
}

function parseResponseBody(responseText: string) {
  if (!responseText) {
    return null;
  }

  try {
    return JSON.parse(responseText) as unknown;
  } catch {
    return responseText;
  }
}

function getErrorMessage(body: unknown, fallback: string) {
  if (typeof body === "string" && body.trim().length > 0) {
    return body;
  }

  if (body && typeof body === "object" && "message" in body) {
    const message = (body as { message?: unknown }).message;
    if (typeof message === "string") {
      return message;
    }
  }

  return fallback || "Request failed";
}
