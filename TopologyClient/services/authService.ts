import { apiRequest } from "./apiClient";
import type { User } from "@/types";

export const authService = {
  publicCheck() {
    return apiRequest<{ message: string }>("/api/auth/public");
  },

  getProfile(accessToken: string) {
    return apiRequest<User>("/api/auth/profile", { accessToken });
  },

  deleteProfile(accessToken: string) {
    return apiRequest<void>("/api/auth/profile", {
      method: "DELETE",
      accessToken
    });
  }
};
