export interface OrchestratorRequest {
  message: string;
  currentPage?: string;
}

export interface OrchestratorResponse {
  message: string;
  requiresConfirmation: boolean;
  delta?: DeltaResult;
  displayData?: DisplayData;
  nextIntent?: string;
}

export interface DeltaResult {
  previousLifetimeTotal: number;
  updatedLifetimeTotal: number;
  previousCurrentYearTotal: number;
  updatedCurrentYearTotal: number;
  previousPresentValue: number;
  updatedPresentValue: number;
  fieldChanged: string;
  previousValue: string;
  newValue: string;
  narrativeSummary: string;
  ltcPresentValueDelta?: number;
}

export interface DisplayData {
  type: string;
  payload?: unknown;
}
