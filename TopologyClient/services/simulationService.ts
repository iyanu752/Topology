import { apiRequest } from "./apiClient";
import type { RunSimulationDto, SimulationResult, SaveDesignDto, SimulationConfiguration } from "@/types";

export const simulationService = {
  preview(design: SaveDesignDto, configuration: SimulationConfiguration, signal?: AbortSignal) {
    return apiRequest<SimulationResult>("/api/simulations/preview", {
      method: "POST", body: { design, configuration }, signal
    });
  },
  runSimulation(roomId: string, dto: RunSimulationDto, accessToken: string) {
    return apiRequest<SimulationResult>(`/api/rooms/${roomId}/simulations/run`, {
      method: "POST",
      accessToken,
      body: dto
    });
  }
};
