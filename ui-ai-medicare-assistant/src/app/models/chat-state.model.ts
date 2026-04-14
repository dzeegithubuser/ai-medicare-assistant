export interface CardSelection {
  nameType: 'brand' | 'generic' | null;
  selectedName: string | null;
  dosageForm: string | null;
  strength: string | null;
  packaging: string | null;
}

export interface ChatMessage {
  role: 'user' | 'assistant' | 'system';
  content: string;
  timestamp: Date;
  /** Relative URL of the page where this message was created. */
  context?: string;
}

export interface ChatDrugSelectionCommand {
  drugName: string | null;
  type: string | null;
  dosageForm: string | null;
  strength: string | null;
  quantity: number | null;
  action: 'select' | 'options' | 'confirm_all' | 'remove' | 'edit';
}

export interface PendingDrugFollowupPrompt {
  drugName: string;
  missingFields: string[];
}

export interface ChatPharmacySelectionCommand {
  pharmacyName: string | null;
  pharmacyNames?: string[] | null;
  action: 'select' | 'remove' | 'list' | 'search' | 'clearFilter';
  searchTerm: string | null;
}

export interface ChatPlanSelectionCommand {
  planName: string | null;
  planCategory: 'partd' | 'medigap' | 'ma' | null;
  action: 'select' | 'remove' | 'list' | 'info';
}

/** Signal to trigger cost analysis from chat (name is set separately via `pendingCostRunRecommendationName`). */
export interface ChatRunAnalysisCommand {
  trigger: true;
}
