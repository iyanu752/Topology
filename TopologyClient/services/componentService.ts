import { apiRequest } from "./apiClient";
import type { ComponentDefinition, ComponentType } from "@/types";

export const componentService = {
  getComponents() {
    return apiRequest<ComponentDefinition[]>("/api/components");
  },

  getComponent(type: ComponentType) {
    return apiRequest<ComponentDefinition>(`/api/components/${type}`);
  },

  seedComponents() {
    return apiRequest<void>("/api/components/seed", { method: "POST" });
  }
};
