// Espelha EIP.Platform.Identity.Api.Contracts (src/Platform/Identity/Api/Contracts/AuthContracts.cs).
// JSON em camelCase — política padrão do ASP.NET Core para Web API.

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  email: string;
  password: string;
  displayName: string;
}

export interface SelectTenantRequest {
  tenantId: string;
}

export interface TenantOption {
  tenantId: string;
  tenantName: string;
  tenantSlug: string;
}

export interface AuthResponse {
  accessToken: string;
  accessTokenExpiresAtUtc: string;
  refreshToken: string;
  requiresTenantSelection: boolean;
  availableTenants: TenantOption[];
}

export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  traceId?: string;
  correlationId?: string;
}
