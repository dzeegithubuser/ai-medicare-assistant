import { Injectable, signal } from '@angular/core';
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  HttpTransportType,
} from '@microsoft/signalr';
import { ReplaySubject, Observable, from, of } from 'rxjs';
import { tap } from 'rxjs/operators';
import { environment } from '../../environments/environment';

export interface SignalRChatMessage {
  role: string;
  content: string;
  timestamp: string;
  /** Relative URL of the page where this message was created. */
  context?: string;
}

export interface SignalRSessionPayload {
  messages: SignalRChatMessage[];
  uiState: { editMode: boolean };
}

/**
 * Manages a single persistent SignalR WebSocket connection to /hubs/chat.
 *
 * Responsibilities:
 *  - connect(token)   — open the hub connection; returns Observable<void> that
 *                       completes once connected.
 *  - disconnect()     — close on sign-out and reset internal state.
 *  - syncMessages()   — invoke SyncMessages hub method (replaces PATCH /api/chat/session/messages).
 *  - session$         — ReplaySubject that emits the ReceiveSession push from the hub
 *                       (replaces GET /api/chat/session).
 */
@Injectable({ providedIn: 'root' })
export class ChatSignalRService {
  private connection: HubConnection | null = null;

  private _sessionSubject = new ReplaySubject<SignalRSessionPayload>(1);

  /** Emits once the hub pushes the stored session on connect. Late subscribers
   *  receive the replayed value immediately (handles timing between sign-in and
   *  dashboard bootstrap). */
  get session$(): Observable<SignalRSessionPayload> {
    return this._sessionSubject.asObservable();
  }

  /** True while the WebSocket connection is in Connected state. */
  readonly isConnected = signal(false);

  /**
   * Start the hub connection using the provided JWT token.
   * If a connection is already active (Connected / Connecting / Reconnecting),
   * returns immediately without opening a second connection.
   */
  connect(token: string): Observable<void> {
    if (
      this.connection &&
      (this.connection.state === HubConnectionState.Connected ||
        this.connection.state === HubConnectionState.Connecting ||
        this.connection.state === HubConnectionState.Reconnecting)
    ) {
      return of(void 0);
    }

    this.connection = new HubConnectionBuilder()
      .withUrl(`${environment.apiUrl}/hubs/chat`, {
        // JWT cannot be set as an HTTP header on WebSocket upgrade requests by
        // browsers, so SignalR reads it from the `access_token` query param instead.
        accessTokenFactory: () => token,
        transport: HttpTransportType.WebSockets,
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
      .build();

    // Server push on connect — replaces GET /api/chat/session
    this.connection.on(
      'ReceiveSession',
      (messages: SignalRChatMessage[], uiState: { editMode: boolean }) => {
        this._sessionSubject.next({
          messages: messages ?? [],
          uiState: uiState ?? { editMode: false },
        });
      }
    );

    this.connection.onreconnected(() => this.isConnected.set(true));
    this.connection.onclose(() => this.isConnected.set(false));

    return from(this.connection.start()).pipe(
      tap(() => this.isConnected.set(true))
    );
  }

  /** Stop the connection and reset state. Call on sign-out. */
  disconnect(): void {
    this.connection?.stop().catch(() => {});
    this.connection = null;
    this.isConnected.set(false);
    // Replace subject so the next login gets a fresh replay.
    this._sessionSubject = new ReplaySubject<SignalRSessionPayload>(1);
  }

  /**
   * Send the full message list to the server over the open WebSocket.
   * Silently no-ops if the connection is not in Connected state.
   */
  syncMessages(messages: SignalRChatMessage[]): void {
    if (
      !this.connection ||
      this.connection.state !== HubConnectionState.Connected
    ) {
      return;
    }
    this.connection.invoke('SyncMessages', messages).catch(() => {});
  }
}
