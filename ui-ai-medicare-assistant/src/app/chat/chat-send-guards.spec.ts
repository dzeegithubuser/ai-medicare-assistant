import { describe, it, expect } from 'vitest';
import {
  isExplicitDrugStepCommand,
  isGenericProfileReviewHoldCommand,
  isNextStepCommand,
  shouldTriggerProfileSaveOnNext,
} from './chat-send-guards';

describe('chat-send-guards', () => {
  it('recognizes next-step commands', () => {
    expect(isNextStepCommand('next')).toBe(true);
    expect(isNextStepCommand('continue')).toBe(true);
    expect(isNextStepCommand('go to next step')).toBe(true);
    expect(isNextStepCommand('go to drug')).toBe(true);
    expect(isNextStepCommand('continue to drugs')).toBe(true);
    expect(isNextStepCommand('move forward')).toBe(true);
    expect(isNextStepCommand('move to next')).toBe(true);
    expect(isNextStepCommand('continue to next step')).toBe(true);
    expect(isNextStepCommand('want to go to next')).toBe(true);
  });

  it('does not treat unrelated text as next-step command', () => {
    expect(isNextStepCommand('zip 80113')).toBe(false);
    expect(isNextStepCommand('show plans')).toBe(false);
    expect(isNextStepCommand('search drug eliquis')).toBe(false);
  });

  it('detects explicit drug-step commands for save/continue path', () => {
    expect(isExplicitDrugStepCommand('go to drug')).toBe(true);
    expect(isExplicitDrugStepCommand('go to drugs')).toBe(true);
    expect(isExplicitDrugStepCommand('continue to drugs')).toBe(true);
    expect(isExplicitDrugStepCommand('next')).toBe(false);
  });

  it('generic profile hold excludes explicit drug commands', () => {
    expect(isGenericProfileReviewHoldCommand('next')).toBe(true);
    expect(isGenericProfileReviewHoldCommand('go to drugs')).toBe(false);
  });

  it('triggers profile save only on profile page in edit mode', () => {
    expect(shouldTriggerProfileSaveOnNext('next', true, true)).toBe(true);
    expect(shouldTriggerProfileSaveOnNext('next', false, true)).toBe(false);
    expect(shouldTriggerProfileSaveOnNext('next', true, false)).toBe(false);
    expect(shouldTriggerProfileSaveOnNext('zip 80113', true, true)).toBe(false);
  });
});
