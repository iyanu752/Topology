import { apiRequest } from "./apiClient";
import type { ComponentType, ConnectionRule } from "@/types";

export const connectionRuleService = {
  getRules() {
    return apiRequest<ConnectionRule[]>("/api/connection-rules");
  },

  getRule(sourceType: ComponentType, targetType: ComponentType) {
    return apiRequest<ConnectionRule>(`/api/connection-rules/${sourceType}/${targetType}`);
  },

  seedRules() {
    return apiRequest<void>("/api/connection-rules/seed", { method: "POST" });
  }
};
