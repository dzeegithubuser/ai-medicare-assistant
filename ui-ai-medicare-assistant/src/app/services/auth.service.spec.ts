import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { AuthService } from './auth.service';
import { AuthResponse, AuthUser } from '../models/auth.model';
import { environment } from '../../environments/environment';

describe('AuthService', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;
  let routerMock: { navigateByUrl: ReturnType<typeof vi.fn> };

  const mockUser: AuthUser = { id: '1', email: 'test@example.com', phone: '555-0100' };
  const mockResponse: AuthResponse = {
    success: true,
    message: 'OK',
    token: 'jwt-token-123',
    user: mockUser,
  };

  beforeEach(() => {
    routerMock = { navigateByUrl: vi.fn() };

    // Clear sessionStorage before each test
    sessionStorage.clear();

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: Router, useValue: routerMock },
      ],
    });

    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
    sessionStorage.clear();
  });

  // ─── HTTP Methods ──────────────────────────────────────────────

  it('should POST to signup endpoint', () => {
    const req = { email: 'a@b.com', phone: '555', password: 'P@ss1', confirmPassword: 'P@ss1' };
    service.signUp(req).subscribe(res => expect(res.success).toBe(true));

    const httpReq = httpMock.expectOne(`${environment.apiUrl}/api/auth/signup`);
    expect(httpReq.request.method).toBe('POST');
    httpReq.flush(mockResponse);
  });

  it('should POST to signin endpoint', () => {
    service.signIn({ email: 'a@b.com', password: 'P@ss1' }).subscribe();

    const httpReq = httpMock.expectOne(`${environment.apiUrl}/api/auth/signin`);
    expect(httpReq.request.method).toBe('POST');
    httpReq.flush(mockResponse);
  });

  it('should POST to forgot-password endpoint', () => {
    service.forgotPassword({ email: 'a@b.com' }).subscribe();

    const httpReq = httpMock.expectOne(`${environment.apiUrl}/api/auth/forgot-password`);
    expect(httpReq.request.method).toBe('POST');
    httpReq.flush(mockResponse);
  });

  it('should POST to reset-password endpoint', () => {
    service.resetPassword({ token: 'tok', newPassword: 'x', confirmPassword: 'x' }).subscribe();

    const httpReq = httpMock.expectOne(`${environment.apiUrl}/api/auth/reset-password`);
    expect(httpReq.request.method).toBe('POST');
    httpReq.flush(mockResponse);
  });

  // ─── handleAuthSuccess ─────────────────────────────────────────

  it('should store token and user in sessionStorage on auth success', () => {
    service.handleAuthSuccess(mockResponse);

    expect(sessionStorage.getItem('auth_token')).toBe('jwt-token-123');
    expect(service.currentUser()).toEqual(mockUser);
    const storedUser = JSON.parse(sessionStorage.getItem('auth_user')!);
    expect(storedUser.email).toBe('test@example.com');
  });

  it('should set currentUser signal on auth success', () => {
    expect(service.currentUser()).toBeNull();
    service.handleAuthSuccess(mockResponse);
    expect(service.currentUser()?.email).toBe('test@example.com');
  });

  // ─── getToken ──────────────────────────────────────────────────

  it('should return null when no token stored', () => {
    expect(service.getToken()).toBeNull();
  });

  it('should return token when valid and within TTL', () => {
    sessionStorage.setItem('auth_token', 'tok');
    sessionStorage.setItem('auth_token_ts', Date.now().toString());

    expect(service.getToken()).toBe('tok');
  });

  it('should return null and sign out when token expired', () => {
    sessionStorage.setItem('auth_token', 'tok');
    // Set timestamp to 2 hours ago (expired)
    sessionStorage.setItem('auth_token_ts', (Date.now() - 2 * 60 * 60 * 1000).toString());

    const result = service.getToken();

    expect(result).toBeNull();
    // signOut clears sessionStorage
    expect(sessionStorage.getItem('auth_token')).toBeNull();
  });

  // ─── isAuthenticated ───────────────────────────────────────────

  it('should be false initially', () => {
    expect(service.isAuthenticated()).toBe(false);
  });

  it('should be true after successful auth with valid token', () => {
    sessionStorage.setItem('auth_token', 'tok');
    sessionStorage.setItem('auth_token_ts', Date.now().toString());
    service.handleAuthSuccess(mockResponse);

    expect(service.isAuthenticated()).toBe(true);
  });

  // ─── signOut ───────────────────────────────────────────────────

  it('should clear sessionStorage and navigate to /signin on signOut', () => {
    service.handleAuthSuccess(mockResponse);

    service.signOut();

    expect(sessionStorage.length).toBe(0);
    expect(service.currentUser()).toBeNull();
    expect(routerMock.navigateByUrl).toHaveBeenCalledWith('/signin', { replaceUrl: true });
  });
});
