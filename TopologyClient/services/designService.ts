import { apiRequest } from "./apiClient";
import type { Design, DesignValidationResult, SaveDesignDto } from "@/types";

export const designService = {
  getDesign(roomId: string, accessToken: string) {
    return apiRequest<Design>(`/api/rooms/${roomId}/design`, { accessToken });
  },

  saveDesign(roomId: string, dto: SaveDesignDto, accessToken: string) {
    return apiRequest<Design>(`/api/rooms/${roomId}/design`, {
      method: "PUT",
      accessToken,
      body: dto
    });
  },

  deleteDesign(roomId: string, accessToken: string) {
    return apiRequest<void>(`/api/rooms/${roomId}/design`, {
      method: "DELETE",
      accessToken
    });
  },

  validateDesign(roomId: string, dto: SaveDesignDto, accessToken: string) {
    return apiRequest<DesignValidationResult>(`/api/rooms/${roomId}/design/validate`, {
      method: "POST",
      accessToken,
      body: dto
    });
  }
};
