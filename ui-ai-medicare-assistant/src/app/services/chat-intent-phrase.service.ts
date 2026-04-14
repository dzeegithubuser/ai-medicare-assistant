import { Injectable } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class ChatIntentPhraseService {
  looksLikeCostEvaluationRequest(text: string): boolean {
    return /\b(cost evaluation|cost projections?|lifetime cost|evaluate.*cost|full\s+cost|show\s+(me\s+)?(a\s+)?cost\s+(breakdown|summary|projection)|go\s+to\s+cost|open\s+cost\s+projection)\b/i.test(
      text,
    );
  }
}
