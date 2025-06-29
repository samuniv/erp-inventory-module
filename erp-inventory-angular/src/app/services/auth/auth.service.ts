import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable, of } from 'rxjs';
import { map, catchError, tap } from 'rxjs/operators';
import { Router } from '@angular/router';

export interface User {
  id: string;
  name: string;
  email: string;
  role: string;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface AuthResponse {
  token: string;
  user: User;
}

@Injectable({
  providedIn: 'root',
})
export class AuthService {
  private readonly TOKEN_KEY = 'erp_auth_token';
  private readonly USER_KEY = 'erp_user_data';
  private readonly API_BASE_URL = 'http://localhost:5010/api'; // Gateway API URL

  private currentUserSubject = new BehaviorSubject<User | null>(
    this.getUserFromStorage()
  );
  public currentUser$ = this.currentUserSubject.asObservable();

  private isAuthenticatedSubject = new BehaviorSubject<boolean>(
    this.hasValidToken()
  );
  public isAuthenticated$ = this.isAuthenticatedSubject.asObservable();

  constructor(private http: HttpClient, private router: Router) {
    // Check token validity on service initialization
    this.checkTokenValidity();
  }

  /**
   * Login user with credentials
   */
  login(credentials: LoginRequest): Observable<boolean> {
    // For development, simulate API call with mock data
    if (this.isDevelopmentMode()) {
      return this.mockLogin(credentials);
    }

    return this.http
      .post<AuthResponse>(`${this.API_BASE_URL}/auth/login`, credentials)
      .pipe(
        tap((response) => {
          this.setAuthData(response.token, response.user);
        }),
        map(() => true),
        catchError((error) => {
          console.error('Login error:', error);
          return of(false);
        })
      );
  }

  /**
   * Logout user and clear stored data
   */
  logout(): void {
    // Clear stored data
    localStorage.removeItem(this.TOKEN_KEY);
    localStorage.removeItem(this.USER_KEY);

    // Update subjects
    this.currentUserSubject.next(null);
    this.isAuthenticatedSubject.next(false);

    // Navigate to login
    this.router.navigate(['/login']);
  }

  /**
   * Get current user
   */
  getCurrentUser(): User | null {
    return this.currentUserSubject.value;
  }

  /**
   * Check if user is authenticated
   */
  isAuthenticated(): boolean {
    return this.isAuthenticatedSubject.value;
  }

  /**
   * Get authentication token
   */
  getToken(): string | null {
    return localStorage.getItem(this.TOKEN_KEY);
  }

  /**
   * Refresh token (placeholder for future implementation)
   */
  refreshToken(): Observable<boolean> {
    const token = this.getToken();
    if (!token) {
      return of(false);
    }

    // TODO: Implement actual token refresh call
    return of(true);
  }

  /**
   * Check if current user has specific role
   */
  hasRole(role: string): boolean {
    const user = this.getCurrentUser();
    return user?.role === role || false;
  }

  /**
   * Check if current user has any of the specified roles
   */
  hasAnyRole(roles: string[]): boolean {
    const user = this.getCurrentUser();
    return user ? roles.includes(user.role) : false;
  }

  // Private helper methods

  private setAuthData(token: string, user: User): void {
    localStorage.setItem(this.TOKEN_KEY, token);
    localStorage.setItem(this.USER_KEY, JSON.stringify(user));

    this.currentUserSubject.next(user);
    this.isAuthenticatedSubject.next(true);
  }

  private getUserFromStorage(): User | null {
    const userData = localStorage.getItem(this.USER_KEY);
    return userData ? JSON.parse(userData) : null;
  }

  private hasValidToken(): boolean {
    const token = localStorage.getItem(this.TOKEN_KEY);
    if (!token) {
      return false;
    }

    // TODO: Add JWT token expiry validation
    try {
      const payload = JSON.parse(atob(token.split('.')[1]));
      const now = Date.now() / 1000;
      return payload.exp > now;
    } catch {
      return true; // For development, assume token is valid
    }
  }

  private checkTokenValidity(): void {
    if (!this.hasValidToken()) {
      this.logout();
    }
  }

  private isDevelopmentMode(): boolean {
    return (
      !this.API_BASE_URL.includes('production') ||
      window.location.hostname === 'localhost'
    );
  }

  private mockLogin(credentials: LoginRequest): Observable<boolean> {
    // Mock login for development
    const mockUsers = [
      {
        id: '1',
        name: 'Admin User',
        email: 'admin@erp.com',
        role: 'Administrator',
      },
      {
        id: '2',
        name: 'Manager User',
        email: 'manager@erp.com',
        role: 'Manager',
      },
      {
        id: '3',
        name: 'Clerk User',
        email: 'clerk@erp.com',
        role: 'Clerk',
      },
    ];

    // Simulate network delay
    return of(null).pipe(
      map(() => {
        // Find user by email
        const user = mockUsers.find((u) => u.email === credentials.email);
        if (!user) {
          throw new Error('Invalid credentials');
        }

        // For demo, any password works
        const mockToken = this.generateMockJWT(user);
        this.setAuthData(mockToken, user);
        return true;
      }),
      catchError(() => of(false))
    );
  }

  private generateMockJWT(user: User): string {
    const header = btoa(JSON.stringify({ alg: 'HS256', typ: 'JWT' }));
    const payload = btoa(
      JSON.stringify({
        sub: user.id,
        email: user.email,
        role: user.role,
        name: user.name,
        iat: Math.floor(Date.now() / 1000),
        exp: Math.floor(Date.now() / 1000) + 24 * 60 * 60, // 24 hours
      })
    );
    const signature = btoa('mock-signature');
    return `${header}.${payload}.${signature}`;
  }
}
