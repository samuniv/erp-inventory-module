import { Component, OnInit, OnDestroy } from '@angular/core';
import { Router, NavigationEnd, RouterOutlet } from '@angular/router';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatButtonModule } from '@angular/material/button';
import { MatMenuModule } from '@angular/material/menu';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatBadgeModule } from '@angular/material/badge';
import { MatDividerModule } from '@angular/material/divider';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { filter, takeUntil } from 'rxjs/operators';
import { Subject } from 'rxjs';
import { AuthService, User } from './services/auth/auth.service';

interface Notification {
  message: string;
  type: 'info' | 'warning';
  timestamp: Date;
}

interface Breadcrumb {
  label: string;
  url: string;
  isLast?: boolean;
}

@Component({
  selector: 'app-root',
  imports: [
    RouterOutlet,
    MatSidenavModule,
    MatToolbarModule,
    MatIconModule,
    MatListModule,
    MatButtonModule,
    MatMenuModule,
    MatFormFieldModule,
    MatInputModule,
    MatBadgeModule,
    MatDividerModule,
    RouterModule,
    FormsModule,
    CommonModule
  ],
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss']
})
export class AppComponent implements OnInit, OnDestroy {
  title = 'ERP Inventory Management';
  showLayout = true;
  
  user: User | null = null;
  searchQuery = '';
  
  notifications: Notification[] = [
    {
      message: 'Low stock alert: Product A has only 5 units remaining',
      type: 'warning',
      timestamp: new Date()
    },
    {
      message: 'New order #12345 has been placed',
      type: 'info',
      timestamp: new Date(Date.now() - 3600000) // 1 hour ago
    }
  ];

  breadcrumbs: Breadcrumb[] = [];
  private destroy$ = new Subject<void>();

  constructor(
    private router: Router,
    private authService: AuthService
  ) {
    // Hide layout for login page
    this.router.events
      .pipe(
        filter(event => event instanceof NavigationEnd),
        takeUntil(this.destroy$)
      )
      .subscribe((event: NavigationEnd) => {
        this.showLayout = !event.url.includes('/login');
      });
  }

  ngOnInit(): void {
    // Subscribe to user changes
    this.authService.currentUser$
      .pipe(takeUntil(this.destroy$))
      .subscribe(user => {
        this.user = user;
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  get notificationCount(): number {
    return this.notifications.length;
  }

  onSearch(): void {
    if (this.searchQuery.trim()) {
      console.log('Searching for:', this.searchQuery);
      // TODO: Implement search functionality
    }
  }

  logout(): void {
    this.authService.logout();
  }
}
