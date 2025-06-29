import { Routes } from '@angular/router';
import { DashboardComponent } from './components/dashboard/dashboard.component';
import { LoginComponent } from './components/auth/login/login.component';
import { NotFoundComponent } from './components/shared/not-found/not-found.component';
import { authGuard } from './guards/auth/auth.guard';

export const routes: Routes = [
  // Public routes (no authentication required)
  {
    path: 'login',
    component: LoginComponent,
    title: 'Login - ERP Inventory System'
  },

  // Protected routes (authentication required)
  {
    path: '',
    canActivate: [authGuard],
    children: [
      {
        path: '',
        redirectTo: '/dashboard',
        pathMatch: 'full'
      },
      {
        path: 'dashboard',
        component: DashboardComponent,
        title: 'Dashboard - ERP Inventory System'
      }
      // TODO: Lazy-loaded feature modules (uncomment when components are created)
      // {
      //   path: 'inventory',
      //   loadComponent: () => import('./components/inventory/inventory.component').then(m => m.InventoryComponent),
      //   title: 'Inventory - ERP Inventory System'
      // },
      // {
      //   path: 'inventory/:id',
      //   loadComponent: () => import('./components/inventory/inventory-detail/inventory-detail.component').then(m => m.InventoryDetailComponent),
      //   title: 'Inventory Details - ERP Inventory System'
      // },
      // {
      //   path: 'orders',
      //   loadComponent: () => import('./components/orders/orders.component').then(m => m.OrdersComponent),
      //   title: 'Orders - ERP Inventory System'
      // },
      // {
      //   path: 'orders/:id',
      //   loadComponent: () => import('./components/orders/order-detail/order-detail.component').then(m => m.OrderDetailComponent),
      //   title: 'Order Details - ERP Inventory System'
      // },
      // {
      //   path: 'suppliers',
      //   loadComponent: () => import('./components/suppliers/suppliers.component').then(m => m.SuppliersComponent),
      //   title: 'Suppliers - ERP Inventory System'
      // },
      // {
      //   path: 'suppliers/:id',
      //   loadComponent: () => import('./components/suppliers/supplier-detail/supplier-detail.component').then(m => m.SupplierDetailComponent),
      //   title: 'Supplier Details - ERP Inventory System'
      // },
      // {
      //   path: 'reports',
      //   loadComponent: () => import('./components/reports/reports.component').then(m => m.ReportsComponent),
      //   title: 'Reports - ERP Inventory System'
      // },
      // {
      //   path: 'settings',
      //   loadComponent: () => import('./components/settings/settings.component').then(m => m.SettingsComponent),
      //   title: 'Settings - ERP Inventory System'
      // }
    ]
  },

  // 404 - Not Found
  {
    path: '404',
    component: NotFoundComponent,
    title: 'Page Not Found - ERP Inventory System'
  },

  // Wildcard route - must be last
  {
    path: '**',
    redirectTo: '/404'
  }
];
