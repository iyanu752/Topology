import { apiRequest } from "./apiClient";
import type { RunSimulationDto, SimulationResult } from "@/types";

export const simulationService = {
  runSimulation(roomId: string, dto: RunSimulationDto, accessToken: string) {
    return apiRequest<SimulationResult>(`/api/rooms/${roomId}/simulations/run`, {
      method: "POST",
      accessToken,
      body: dto
    });
  }
};
