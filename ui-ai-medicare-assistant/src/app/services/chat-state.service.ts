import { Injectable, signal, inject } from '@angular/core';
import { Router } from '@angular/router';
import { ChatSignalRService } from './chat-signal-r.service';
import { ChatMessage } from '../models/chat-state.model';

/**
 * Cross-cutting chat state shared by Medicare and LTC analysis flows.
 * Owns the message array, loading flag, SignalR sync, and session persistence for messages.
 */
@Injectable({ providedIn: 'root' })
export class ChatStateService {
  private chatSignalR = inject(ChatSignalRService);
  private router = inject(Router);

  private static readonly MESSAGES_STORAGE_KEY = 'chat-messages-state';

  readonly messages = signal<ChatMessage[]>([]);
  readonly isLoading = signal(false);

  /**
   * Incremented by analysis reset flows. ChatComponent watches this via effect()
   * to reset the wizard state whenever an analysis is cleared.
   */
  readonly wizardResetTrigger = signal(0);

  /** Timer handle for debouncing rapid message bursts before sending over SignalR. */
  private syncTimer: ReturnType<typeof setTimeout> | null = null;

  // ─── Messages ──────────────────────────────────────────────────

  addUserMessage(content: string) {
    const context = this.router.url;
    this.messages.update(msgs => [...msgs, { role: 'user', content, timestamp: new Date(), context }]);
    this.persistMessages();
    this.syncMessagesToServer();
  }

  addAssistantMessage(content: string) {
    const context = this.router.url;
    this.messages.update(msgs => [...msgs, { role: 'assistant', content, timestamp: new Date(), context }]);
    this.persistMessages();
    this.syncMessagesToServer();
  }

  replaceLastAssistantMessage(content: string) {
    const context = this.router.url;
    this.messages.update(msgs => {
      let idx = -1;
      for (let i = msgs.length - 1; i >= 0; i--) {
        if (msgs[i].role === 'assistant') { idx = i; break; }
      }
      if (idx >= 0) {
        const updated = [...msgs];
        updated[idx] = { ...updated[idx], content, timestamp: new Date(), context };
        return updated;
      }
      return [...msgs, { role: 'assistant', content, timestamp: new Date(), context }];
    });
    this.persistMessages();
    this.syncMessagesToServer();
  }

  addSystemMessage(content: string) {
    const context = this.router.url;
    this.messages.update(msgs => [...msgs, { role: 'system', content, timestamp: new Date(), context }]);
    this.persistMessages();
    this.syncMessagesToServer();
  }

  removeAssistantMessagesContaining(text: string) {
    if (!text?.trim()) return;
    this.messages.update(msgs =>
      msgs.filter(m => !(m.role === 'assistant' && m.content.includes(text)))
    );
    this.persistMessages();
    this.syncMessagesToServer();
  }

  hydrateMessagesFromServer(messages: ChatMessage[]) {
    this.messages.set(messages);
    this.persistMessages();
  }

  setLoading(loading: boolean) {
    this.isLoading.set(loading);
  }

  /** Signal wizard / chat component to reset after an analysis clear. */
  triggerReset() {
    this.wizardResetTrigger.update(v => v + 1);
  }

  /** Hard clear used by logout: wipes chat messages and sessionStorage key. */
  clearMessagesForSignOut() {
    this.messages.set([]);
    sessionStorage.removeItem(ChatStateService.MESSAGES_STORAGE_KEY);
    this.triggerReset();
  }

  // ─── Session Storage Persistence (messages only) ───────────────

  private persistMessages() {
    try {
      const payload = this.messages().map(m => ({
        role: m.role,
        content: m.content,
        timestamp: m.timestamp instanceof Date ? m.timestamp.toISOString() : m.timestamp,
        context: m.context,
      }));
      sessionStorage.setItem(ChatStateService.MESSAGES_STORAGE_KEY, JSON.stringify(payload));
    } catch { /* quota exceeded — silently skip */ }
  }

  /**
   * One-time migration: if the old combined key has messages but the new key doesn't,
   * pull messages from the old key into the new key.
   */
  migrateFromLegacyKey() {
    const newRaw = sessionStorage.getItem(ChatStateService.MESSAGES_STORAGE_KEY);
    if (newRaw) return; // already migrated
    try {
      const oldRaw = sessionStorage.getItem('drug-analysis-state');
      if (!oldRaw) return;
      const old = JSON.parse(oldRaw);
      if (Array.isArray(old?.messages) && old.messages.length > 0) {
        sessionStorage.setItem(ChatStateService.MESSAGES_STORAGE_KEY, JSON.stringify(old.messages));
        // Remove messages from old key to save space
        delete old.messages;
        sessionStorage.setItem('drug-analysis-state', JSON.stringify(old));
      }
    } catch { /* ignore */ }
  }

  // ─── SignalR Sync ──────────────────────────────────────────────

  private syncMessagesToServer() {
    if (!sessionStorage.getItem('auth_token')) return;
    if (this.syncTimer !== null) clearTimeout(this.syncTimer);
    this.syncTimer = setTimeout(() => {
      this.syncTimer = null;
      const payload = this.messages().map(m => ({
        role: m.role,
        content: m.content,
        timestamp: m.timestamp instanceof Date ? m.timestamp.toISOString() : String(m.timestamp),
        context: m.context,
      }));
      this.chatSignalR.syncMessages(payload);
    }, 500);
  }
}
