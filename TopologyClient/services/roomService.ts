import { apiRequest } from "./apiClient";
import type { CreateRoomDto, DeleteRoomDto, JoinRoomDto, Room } from "@/types";

export const roomService = {
  createRoom(dto: CreateRoomDto, accessToken: string) {
    return apiRequest<Room>("/api/room/create", {
      method: "POST",
      accessToken,
      body: dto
    });
  },

  joinRoom(dto: JoinRoomDto, accessToken: string) {
    return apiRequest<Room>("/api/room/join", {
      method: "POST",
      accessToken,
      body: dto
    });
  },

  deleteRoom(dto: DeleteRoomDto, accessToken: string) {
    return apiRequest<void>("/api/room", {
      method: "DELETE",
      accessToken,
      body: dto
    });
  }
};
