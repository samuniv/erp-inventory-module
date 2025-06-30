import { Component, OnInit, OnDestroy, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormControl } from '@angular/forms';
import { MatTableModule, MatTableDataSource } from '@angular/material/table';
import { MatPaginatorModule, MatPaginator } from '@angular/material/paginator';
import { MatSortModule, MatSort } from '@angular/material/sort';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import {
  Subject,
  debounceTime,
  distinctUntilChanged,
  takeUntil,
  startWith,
} from 'rxjs';

import { SupplierService } from '../../../services/supplier.service';
import { Supplier, SupplierQueryParams } from '../../../models/supplier.model';
import { SupplierFormDialogComponent } from '../supplier-form-dialog/supplier-form-dialog.component';

@Component({
  selector: 'app-supplier-list',
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatTableModule,
    MatPaginatorModule,
    MatSortModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatCardModule,
    MatTooltipModule,
    MatChipsModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './supplier-list.component.html',
  styleUrl: './supplier-list.component.scss',
})
export class SupplierListComponent implements OnInit, OnDestroy {
  @ViewChild(MatPaginator) paginator!: MatPaginator;
  @ViewChild(MatSort) sort!: MatSort;

  displayedColumns: string[] = [
    'name',
    'contactPerson',
    'email',
    'phone',
    'address',
    'status',
    'actions',
  ];

  dataSource = new MatTableDataSource<Supplier>([]);
  totalCount = 0;
  pageSize = 10;
  currentPage = 0;

  // Filter controls
  searchControl = new FormControl('');
  statusControl = new FormControl('');

  isLoading = false;

  private destroy$ = new Subject<void>();

  constructor(
    private supplierService: SupplierService,
    private dialog: MatDialog,
    private snackBar: MatSnackBar
  ) {}

  ngOnInit(): void {
    this.setupFilters();
    this.loadSuppliers();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private setupFilters(): void {
    // Setup reactive filtering
    this.searchControl.valueChanges
      .pipe(
        startWith(''),
        debounceTime(300),
        distinctUntilChanged(),
        takeUntil(this.destroy$)
      )
      .subscribe(() => this.applyFilters());

    this.statusControl.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => this.applyFilters());
  }

  private loadSuppliers(): void {
    this.isLoading = true;

    const params: SupplierQueryParams = {
      page: this.currentPage + 1,
      pageSize: this.pageSize,
    };

    // Apply filters
    const searchTerm = this.searchControl.value;
    if (searchTerm) {
      params.searchTerm = searchTerm;
    }

    const status = this.statusControl.value;
    if (status !== '') {
      params.isActive = status === 'active';
    }

    this.supplierService
      .getSuppliers(params)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (response) => {
          this.dataSource.data = response.items;
          this.totalCount = response.totalCount;
          this.isLoading = false;
        },
        error: (error) => {
          console.error('Error loading suppliers:', error);
          this.snackBar.open('Error loading suppliers', 'Close', {
            duration: 3000,
          });
          this.isLoading = false;
        },
      });
  }

  private applyFilters(): void {
    this.currentPage = 0;
    if (this.paginator) {
      this.paginator.pageIndex = 0;
    }
    this.loadSuppliers();
  }

  onPageChange(event: any): void {
    this.currentPage = event.pageIndex;
    this.pageSize = event.pageSize;
    this.loadSuppliers();
  }

  onAddSupplier(): void {
    const dialogRef = this.dialog.open(SupplierFormDialogComponent, {
      width: '600px',
      maxWidth: '95vw',
      data: { mode: 'create' },
      disableClose: true,
    });

    dialogRef.afterClosed().subscribe((result) => {
      if (result) {
        this.isLoading = true;
        this.supplierService
          .createSupplier(result)
          .pipe(takeUntil(this.destroy$))
          .subscribe({
            next: (supplier) => {
              this.snackBar.open('Supplier created successfully!', 'Close', {
                duration: 3000,
                panelClass: ['success-snackbar'],
              });
              this.loadSuppliers();
            },
            error: (error) => {
              console.error('Error creating supplier:', error);
              this.snackBar.open(
                'Error creating supplier. Please try again.',
                'Close',
                {
                  duration: 5000,
                  panelClass: ['error-snackbar'],
                }
              );
              this.isLoading = false;
            },
          });
      }
    });
  }

  onEditSupplier(supplier: Supplier): void {
    const dialogRef = this.dialog.open(SupplierFormDialogComponent, {
      width: '600px',
      maxWidth: '95vw',
      data: { mode: 'edit', supplier: { ...supplier } },
      disableClose: true,
    });

    dialogRef.afterClosed().subscribe((result) => {
      if (result) {
        this.isLoading = true;
        this.supplierService
          .updateSupplier(supplier.id, result)
          .pipe(takeUntil(this.destroy$))
          .subscribe({
            next: (updatedSupplier) => {
              this.snackBar.open('Supplier updated successfully!', 'Close', {
                duration: 3000,
                panelClass: ['success-snackbar'],
              });
              this.loadSuppliers();
            },
            error: (error) => {
              console.error('Error updating supplier:', error);
              this.snackBar.open(
                'Error updating supplier. Please try again.',
                'Close',
                {
                  duration: 5000,
                  panelClass: ['error-snackbar'],
                }
              );
              this.isLoading = false;
            },
          });
      }
    });
  }

  onDeleteSupplier(supplier: Supplier): void {
    const confirmMessage = `Are you sure you want to delete supplier "${supplier.name}"? This action cannot be undone.`;

    if (confirm(confirmMessage)) {
      this.isLoading = true;
      this.supplierService
        .deleteSupplier(supplier.id)
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: () => {
            this.snackBar.open('Supplier deleted successfully!', 'Close', {
              duration: 3000,
              panelClass: ['success-snackbar'],
            });
            this.loadSuppliers();
          },
          error: (error) => {
            console.error('Error deleting supplier:', error);
            this.snackBar.open(
              'Error deleting supplier. Please try again.',
              'Close',
              {
                duration: 5000,
                panelClass: ['error-snackbar'],
              }
            );
            this.isLoading = false;
          },
        });
    }
  }

  onToggleStatus(supplier: Supplier): void {
    const updatedSupplier = {
      ...supplier,
      isActive: !supplier.isActive,
    };

    this.isLoading = true;
    this.supplierService
      .updateSupplier(supplier.id, updatedSupplier)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (result) => {
          const statusText = result.isActive ? 'activated' : 'deactivated';
          this.snackBar.open(`Supplier ${statusText} successfully!`, 'Close', {
            duration: 3000,
            panelClass: ['success-snackbar'],
          });
          this.loadSuppliers();
        },
        error: (error) => {
          console.error('Error toggling supplier status:', error);
          this.snackBar.open(
            'Error updating supplier status. Please try again.',
            'Close',
            {
              duration: 5000,
              panelClass: ['error-snackbar'],
            }
          );
          this.isLoading = false;
        },
      });
  }

  clearFilters(): void {
    this.searchControl.setValue('');
    this.statusControl.setValue('');
  }

  getStatusColor(isActive: boolean): string {
    return isActive ? 'primary' : 'warn';
  }

  getStatusText(isActive: boolean): string {
    return isActive ? 'Active' : 'Inactive';
  }
}
