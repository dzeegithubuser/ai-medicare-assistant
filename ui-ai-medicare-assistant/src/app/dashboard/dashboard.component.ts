import { Component, inject, signal, computed, OnInit, DestroyRef } from '@angular/core';
import { Router, RouterOutlet, NavigationEnd } from '@angular/router';
import { filter } from 'rxjs/operators';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { ChatComponent } from '../chat/chat.component';
import { AuthService } from '../services/auth.service';
import { ProfileService } from '../services/profile.service';
import { RecommendationStateService } from '../services/recommendation-state.service';
import { ChatSignalRService } from '../services/chat-signal-r.service';
import { DrugStateService } from '../services/drug-state.service';
import { ChatAnalysisSelectionHydrationService } from '../services/chat-analysis-selection-hydration.service';
import { HttpLoaderService } from '../services/http-loader.service';
import { LtcStateService } from '../long-term-care/ltc-state.service';
import { catchError, forkJoin, map, Observable, of, switchMap, take, timeout } from 'rxjs';
import { AppRoutes } from '../app-routes.const';


@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [
    RouterOutlet,
    MatIconModule, MatButtonModule, MatTooltipModule, MatMenuModule, MatProgressSpinnerModule,
    ChatComponent
  ],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss'
})
export class DashboardComponent implements OnInit {
  protected auth = inject(AuthService);
  protected profileService = inject(ProfileService);
  private router = inject(Router);
  private recommendationState = inject(RecommendationStateService);
  private chatSignalR = inject(ChatSignalRService);
  private drugState = inject(DrugStateService);
  private selectionHydrator = inject(ChatAnalysisSelectionHydrationService);
  protected httpLoader = inject(HttpLoaderService);
  private ltcState = inject(LtcStateService);
  private destroyRef = inject(DestroyRef);

  protected bootstrapReady = signal(false);
  protected showChat = signal(false);

  protected displayName = computed(() => {
    const profile = this.profileService.profile()?.profile;
    if (profile?.firstName) {
      const lastInitial = profile.lastName ? ` ${profile.lastName.charAt(0)}` : '';
      return `${profile.firstName}${lastInitial}`;
    }
    return this.auth.currentUser()?.email ?? '';
  });

  ngOnInit() {
    this.bootstrapDashboardState();
    this.showChat.set(this.isChatRoute(this.router.url));
    this.router.events.pipe(
      filter((e): e is NavigationEnd => e instanceof NavigationEnd),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(e => {
      const isAnalysis = this.isChatRoute(e.urlAfterRedirects);
      this.showChat.set(isAnalysis);
      // Connect SignalR lazily when first navigating into an analysis route
      if (isAnalysis && !this.chatSignalR.isConnected()) {
        this.hydrateChatSession$().pipe(catchError(() => of(null))).subscribe();
      }
    });
  }

  signOut() {
    this.auth.signOut();
  }

  changePassword() {
    this.router.navigate([AppRoutes.abs.CHANGE_PASSWORD]);
  }

  goToRecommendations() {
    this.drugState.resetAll();
    this.ltcState.resetAll();
    this.router.navigate([AppRoutes.abs.SAVED]);
  }

  private bootstrapDashboardState(): void {
    // Only connect SignalR on bootstrap if starting on an analysis route
    const sessionStream$ = this.isChatRoute(this.router.url)
      ? this.hydrateChatSession$().pipe(catchError(() => of(null)))
      : of(null);

    forkJoin({
      profile: this.profileService.loadProfile().pipe(catchError(() => of(null))),
      recommendation: this.recommendationState.loadActiveRecommendation$().pipe(catchError(() => of(null))),
      session: sessionStream$,
    }).pipe(
      switchMap(() => this.selectionHydrator.hydrateAllFromActiveRecommendationSelectionForBootstrap$().pipe(
        catchError(() => of(void 0))
      )),
      map(() => void 0)
    ).subscribe({
      next: () => this.bootstrapReady.set(true),
      error: () => this.bootstrapReady.set(true),
    });
  }

  private isChatRoute(url: string): boolean {
    if (url.startsWith(AppRoutes.abs.COST_PROJECTIONS)) return false;
    if (url.startsWith(AppRoutes.abs.LTC_PROJECTION)) return false;
    return url.startsWith(AppRoutes.abs.MEDICARE_ANALYSIS)
      || url.startsWith(AppRoutes.abs.LTC);
  }

  /**
   * Open the SignalR hub connection and wait for the server to push the stored
   * chat session (ReceiveSession event).  Replaces the old HTTP GET approach:
   *
   *   connect → OnConnectedAsync → hub pushes ReceiveSession → hydrate messages
   *
   * A 5 s timeout protects the forkJoin bootstrap from hanging if the hub is
   * unreachable.  The ReplaySubject(1) inside ChatSignalRService ensures that
   * if the push arrived before we subscribed (e.g. sign-in path) the value is
   * replayed immediately.
   */
  private hydrateChatSession$(): Observable<void> {
    const token = this.auth.getToken();
    if (!token) return of(void 0);

    return this.chatSignalR.connect(token).pipe(
      switchMap(() =>
        this.chatSignalR.session$.pipe(
          take(1),
          timeout(5000),
          catchError(() => of(null))
        )
      ),
      map(session => {
        if (!session) return;
        if (this.drugState.messages().length === 0 && session.messages.length > 0) {
          this.drugState.hydrateMessagesFromServer(
            session.messages.map(m => ({
              role: m.role as 'user' | 'assistant' | 'system',
              content: m.content,
              timestamp: new Date(m.timestamp),
              context: m.context,
            }))
          );
        }
      })
    );
  }
}
