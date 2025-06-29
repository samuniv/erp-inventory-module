import { Component, OnInit, OnDestroy, ViewChild, Inject } from '@angular/core';
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
import {
  MatDialog,
  MatDialogModule,
  MatDialogRef,
  MAT_DIALOG_DATA,
} from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import {
  Subject,
  debounceTime,
  distinctUntilChanged,
  takeUntil,
  startWith,
} from 'rxjs';

import { InventoryService } from '../../../services/inventory.service';
import {
  InventoryItem,
  InventoryQueryParams,
} from '../../../models/inventory.model';
import {
  InventoryFormDialogComponent,
  InventoryFormDialogData,
} from '../inventory-form-dialog/inventory-form-dialog.component';

@Component({
  selector: 'app-inventory-list',
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
  templateUrl: './inventory-list.component.html',
  styleUrl: './inventory-list.component.scss',
})
export class InventoryListComponent implements OnInit, OnDestroy {
  @ViewChild(MatPaginator) paginator!: MatPaginator;
  @ViewChild(MatSort) sort!: MatSort;

  displayedColumns: string[] = [
    'name',
    'sku',
    'category',
    'price',
    'stockLevel',
    'reorderThreshold',
    'supplierName',
    'status',
    'actions',
  ];

  dataSource = new MatTableDataSource<InventoryItem>([]);
  totalCount = 0;
  pageSize = 10;
  currentPage = 0;

  // Filter controls
  searchControl = new FormControl('');
  categoryControl = new FormControl('');
  statusControl = new FormControl('');

  categories: string[] = [];
  suppliers: { id: number; name: string }[] = [];
  isLoading = false;

  private destroy$ = new Subject<void>();

  constructor(
    private inventoryService: InventoryService,
    private dialog: MatDialog,
    private snackBar: MatSnackBar
  ) {}

  ngOnInit(): void {
    this.loadCategories();
    this.loadSuppliers();
    this.setupFilters();
    this.loadInventoryItems();
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

    this.categoryControl.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => this.applyFilters());

    this.statusControl.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe(() => this.applyFilters());
  }

  private loadCategories(): void {
    this.inventoryService
      .getCategories()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (categories) => {
          this.categories = categories;
        },
        error: (error) => {
          console.error('Error loading categories:', error);
          // Set default categories as fallback
          this.categories = [
            'Electronics',
            'Office Supplies',
            'Industrial',
            'Software',
            'Hardware',
          ];
        },
      });
  }

  private loadSuppliers(): void {
    // For now, we'll use mock data since supplier service may not be ready
    // TODO: Replace with actual supplier service call when available
    this.suppliers = [
      { id: 1, name: 'Global Electronics Supply Co.' },
      { id: 2, name: 'TechComponents Ltd.' },
      { id: 3, name: 'European Parts Distribution' },
    ];
  }

  private loadInventoryItems(): void {
    this.isLoading = true;

    const params: InventoryQueryParams = {
      page: this.currentPage + 1,
      pageSize: this.pageSize,
    };

    // Apply filters
    const searchTerm = this.searchControl.value;
    if (searchTerm) {
      params.searchTerm = searchTerm;
    }

    const category = this.categoryControl.value;
    if (category) {
      params.category = category;
    }

    const status = this.statusControl.value;
    if (status !== '') {
      params.isActive = status === 'active';
    }

    this.inventoryService
      .getInventoryItems(params)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (response) => {
          this.dataSource.data = response.items;
          this.totalCount = response.totalCount;
          this.isLoading = false;
        },
        error: (error) => {
          console.error('Error loading inventory items:', error);
          this.snackBar.open('Error loading inventory items', 'Close', {
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
    this.loadInventoryItems();
  }

  onPageChange(event: any): void {
    this.currentPage = event.pageIndex;
    this.pageSize = event.pageSize;
    this.loadInventoryItems();
  }

  onAddItem(): void {
    const dialogData: InventoryFormDialogData = {
      mode: 'create',
      categories: this.categories,
      suppliers: this.suppliers,
    };

    const dialogRef = this.dialog.open(InventoryFormDialogComponent, {
      width: '600px',
      maxWidth: '90vw',
      data: dialogData,
      disableClose: true,
    });

    dialogRef.afterClosed().subscribe((result) => {
      if (result?.action === 'create') {
        this.inventoryService
          .createInventoryItem(result.data)
          .pipe(takeUntil(this.destroy$))
          .subscribe({
            next: (newItem) => {
              this.snackBar.open(
                `${newItem.name} created successfully!`,
                'Close',
                { duration: 3000 }
              );
              this.loadInventoryItems(); // Refresh the list
            },
            error: (error) => {
              console.error('Error creating item:', error);
              this.snackBar.open('Error creating item', 'Close', {
                duration: 3000,
              });
            },
          });
      }
    });
  }

  onEditItem(item: InventoryItem): void {
    const dialogData: InventoryFormDialogData = {
      mode: 'edit',
      item: item,
      categories: this.categories,
      suppliers: this.suppliers,
    };

    const dialogRef = this.dialog.open(InventoryFormDialogComponent, {
      width: '600px',
      maxWidth: '90vw',
      data: dialogData,
      disableClose: true,
    });

    dialogRef.afterClosed().subscribe((result) => {
      if (result?.action === 'update') {
        this.inventoryService
          .updateInventoryItem(item.id, result.data)
          .pipe(takeUntil(this.destroy$))
          .subscribe({
            next: (updatedItem) => {
              this.snackBar.open(
                `${updatedItem.name} updated successfully!`,
                'Close',
                { duration: 3000 }
              );
              this.loadInventoryItems(); // Refresh the list
            },
            error: (error) => {
              console.error('Error updating item:', error);
              this.snackBar.open('Error updating item', 'Close', {
                duration: 3000,
              });
            },
          });
      }
    });
  }

  onDeleteItem(item: InventoryItem): void {
    const confirmDialog = this.dialog.open(ConfirmDeleteDialogComponent, {
      width: '400px',
      data: { itemName: item.name },
    });

    confirmDialog.afterClosed().subscribe((confirmed) => {
      if (confirmed) {
        this.inventoryService
          .deleteInventoryItem(item.id)
          .pipe(takeUntil(this.destroy$))
          .subscribe({
            next: () => {
              this.snackBar.open(
                `${item.name} deleted successfully!`,
                'Close',
                { duration: 3000 }
              );
              this.loadInventoryItems(); // Refresh the list
            },
            error: (error) => {
              console.error('Error deleting item:', error);
              this.snackBar.open('Error deleting item', 'Close', {
                duration: 3000,
              });
            },
          });
      }
    });
  }

  onAdjustStock(item: InventoryItem): void {
    // TODO: Create stock adjustment dialog
    this.snackBar.open(
      `Stock adjustment for ${item.name} coming soon!`,
      'Close',
      { duration: 2000 }
    );
  }

  isLowStock(item: InventoryItem): boolean {
    return item.stockLevel <= item.reorderThreshold;
  }

  getStockStatusColor(item: InventoryItem): string {
    if (item.stockLevel === 0) return 'warn';
    if (this.isLowStock(item)) return 'accent';
    return 'primary';
  }

  clearFilters(): void {
    this.searchControl.setValue('');
    this.categoryControl.setValue('');
    this.statusControl.setValue('');
  }
}

// Confirmation Dialog Component
@Component({
  selector: 'app-confirm-delete-dialog',
  template: `
    <div class="confirm-dialog">
      <h2 mat-dialog-title>Confirm Delete</h2>
      <div mat-dialog-content>
        <p>
          Are you sure you want to delete <strong>{{ data.itemName }}</strong
          >?
        </p>
        <p class="warning-text">This action cannot be undone.</p>
      </div>
      <div mat-dialog-actions>
        <button mat-button (click)="onCancel()">Cancel</button>
        <button mat-raised-button color="warn" (click)="onConfirm()">
          <mat-icon>delete</mat-icon>
          Delete
        </button>
      </div>
    </div>
  `,
  styles: [
    `
      .confirm-dialog {
        .warning-text {
          color: #f44336;
          font-size: 0.9em;
          margin-top: 8px;
        }
      }

      [mat-dialog-actions] {
        display: flex;
        justify-content: flex-end;
        gap: 8px;
      }
    `,
  ],
  imports: [MatDialogModule, MatButtonModule, MatIconModule],
})
export class ConfirmDeleteDialogComponent {
  constructor(
    private dialogRef: MatDialogRef<ConfirmDeleteDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: { itemName: string }
  ) {}

  onConfirm(): void {
    this.dialogRef.close(true);
  }

  onCancel(): void {
    this.dialogRef.close(false);
  }
}
