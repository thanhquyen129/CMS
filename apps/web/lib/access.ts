import { redirect } from "next/navigation";
import { getApiInternalUrl } from "./auth";
import { getSessionToken } from "./api";
import type { ApiResult } from "./bills";

export type AccessRole = {
  id: string;
  code: string;
  name: string;
  isSystem: boolean;
  summaryVi: string | null;
  createdAt: string;
};

export type AccessUser = {
  id: string;
  email: string;
  displayName: string;
  isActive: boolean;
  organizationId: string | null;
  createdAt: string;
};

export type UserRoleItem = {
  roleId: string;
  code: string;
  name: string;
  isSystem: boolean;
};

export type PermissionMatrixItem = {
  actionCode: string;
  permissionName: string;
  enabled: boolean;
  dataScope: string | null;
  locked: boolean;
};

async function apiGet<T>(path: string): Promise<ApiResult<T>> {
  const token = await getSessionToken();
  if (!token) {
    redirect("/login");
  }

  try {
    const res = await fetch(`${getApiInternalUrl()}${path}`, {
      headers: {
        Authorization: `Bearer ${token}`,
        Accept: "application/json",
      },
      cache: "no-store",
    });

    if (res.status === 401) {
      redirect("/login");
    }

    if (!res.ok) {
      const body = (await res.json().catch(() => ({}))) as { message?: string };
      return {
        ok: false,
        status: res.status,
        message:
          body.message ||
          (res.status === 403
            ? "Bạn không có quyền quản trị phân quyền."
            : "Không tải được dữ liệu phân quyền."),
      };
    }

    return { ok: true, data: (await res.json()) as T };
  } catch {
    return {
      ok: false,
      status: 0,
      message: "Không kết nối được máy chủ API. Thử lại sau.",
    };
  }
}

export function listAccessRoles(): Promise<ApiResult<AccessRole[]>> {
  return apiGet<AccessRole[]>("/api/roles");
}

export function listAccessUsers(): Promise<ApiResult<AccessUser[]>> {
  return apiGet<AccessUser[]>("/api/users");
}

export function listUserRoles(userId: string): Promise<ApiResult<UserRoleItem[]>> {
  return apiGet<UserRoleItem[]>(`/api/users/${userId}/roles`);
}

export function getRolePermissionMatrix(
  roleId: string
): Promise<ApiResult<PermissionMatrixItem[]>> {
  return apiGet<PermissionMatrixItem[]>(`/api/roles/${roleId}/permission-matrix`);
}
