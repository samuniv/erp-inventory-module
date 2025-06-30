import { Component, Input, OnInit, OnDestroy } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBarModule } from '@angular/material/snack-bar';
import { MatBadgeModule } from '@angular/material/badge';
import { Subject, takeUntil } from 'rxjs';
import { OrderStateService } from '../../../../services/order-state.service';
import { InventoryService } from '../../../../services/inventory.service';
import {
  InventoryItem,
  InventoryListResponse,
} from '../../../../models/inventory.model';
import { SelectedOrderItem } from '../../../../models/order.model';
import { MatSnackBar } from '@angular/material/snack-bar';

@Component({
  selector: 'app-item-selection-step',
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatCardModule,
    MatIconModule,
    MatTableModule,
    MatChipsModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
    MatBadgeModule,
  ],
  templateUrl: './item-selection-step.component.html',
  styleUrl: './item-selection-step.component.scss',
})
export class ItemSelectionStepComponent implements OnInit, OnDestroy {
  @Input() formGroup!: FormGroup;

  private destroy$ = new Subject<void>();
  searchFormGroup!: FormGroup;

  // Data properties
  inventoryItems: InventoryItem[] = [];
  filteredItems: InventoryItem[] = [];
  selectedItems: SelectedOrderItem[] = [];
  isLoadingItems = false;

  // Table configuration
  displayedColumns: string[] = [
    'name',
    'sku',
    'price',
    'stockLevel',
    'actions',
  ];
  selectedDisplayedColumns: string[] = [
    'name',
    'quantity',
    'unitPrice',
    'totalPrice',
    'actions',
  ];

  constructor(
    private formBuilder: FormBuilder,
    private orderStateService: OrderStateService,
    private inventoryService: InventoryService,
    private snackBar: MatSnackBar
  ) {}

  ngOnInit(): void {
    this.initializeForm();
    this.subscribeToOrderState();
    this.loadInventoryItems();
    this.setupSearchSubscription();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private initializeForm(): void {
    this.searchFormGroup = this.formBuilder.group({
      searchTerm: [''],
    });
  }

  private subscribeToOrderState(): void {
    this.orderStateService.orderState$
      .pipe(takeUntil(this.destroy$))
      .subscribe((state) => {
        this.selectedItems = state.selectedItems || [];
      });
  }

  private loadInventoryItems(): void {
    this.isLoadingItems = true;
    this.inventoryService
      .getInventoryItems({ page: 1, pageSize: 100 })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (response: InventoryListResponse) => {
          this.inventoryItems = response.items;
          this.filteredItems = [...this.inventoryItems];
          this.isLoadingItems = false;
        },
        error: (error) => {
          console.error('Error loading inventory items:', error);
          this.snackBar.open('Error loading inventory items', 'Close', {
            duration: 3000,
          });
          this.isLoadingItems = false;
        },
      });
  }

  private setupSearchSubscription(): void {
    this.searchFormGroup
      .get('searchTerm')
      ?.valueChanges.pipe(takeUntil(this.destroy$))
      .subscribe((searchTerm) => {
        this.filterItems(searchTerm);
      });
  }

  private filterItems(searchTerm: string): void {
    if (!searchTerm?.trim()) {
      this.filteredItems = [...this.inventoryItems];
      return;
    }

    const term = searchTerm.toLowerCase().trim();
    this.filteredItems = this.inventoryItems.filter(
      (item) =>
        item.name.toLowerCase().includes(term) ||
        item.description?.toLowerCase().includes(term) ||
        item.sku.toLowerCase().includes(term)
    );
  }

  // Item selection methods
  addItemToOrder(item: InventoryItem, quantity: number = 1): void {
    if (quantity <= 0 || quantity > item.stockLevel) {
      this.snackBar.open('Invalid quantity selected', 'Close', {
        duration: 3000,
      });
      return;
    }

    const selectedItem: SelectedOrderItem = {
      inventoryItemId: item.id,
      inventoryItemName: item.name,
      availableQuantity: item.stockLevel,
      unitPrice: item.price,
      selectedQuantity: quantity,
      totalPrice: item.price * quantity,
    };

    this.orderStateService.addItem(selectedItem);
    this.snackBar.open(`Added ${item.name} to order`, 'Close', {
      duration: 2000,
    });
  }

  removeItemFromOrder(item: SelectedOrderItem): void {
    this.orderStateService.removeItem(item.inventoryItemId);
    this.snackBar.open(
      `Removed ${item.inventoryItemName} from order`,
      'Close',
      { duration: 2000 }
    );
  }

  updateItemQuantity(item: SelectedOrderItem, newQuantity: number): void {
    if (newQuantity <= 0) {
      this.removeItemFromOrder(item);
      return;
    }

    if (newQuantity > item.availableQuantity) {
      this.snackBar.open('Quantity exceeds available stock', 'Close', {
        duration: 3000,
      });
      return;
    }

    this.orderStateService.updateItemQuantity(
      item.inventoryItemId,
      newQuantity
    );
  }

  // Helper methods
  isItemSelected(item: InventoryItem): boolean {
    return this.selectedItems.some(
      (selected) => selected.inventoryItemId === item.id
    );
  }

  getSelectedItemQuantity(item: InventoryItem): number {
    const selectedItem = this.selectedItems.find(
      (selected) => selected.inventoryItemId === item.id
    );
    return selectedItem?.selectedQuantity || 0;
  }

  formatCurrency(amount: number): string {
    return new Intl.NumberFormat('en-US', {
      style: 'currency',
      currency: 'USD',
    }).format(amount);
  }

  get totalSelectedItems(): number {
    return this.selectedItems.length;
  }

  get totalOrderValue(): number {
    return this.selectedItems.reduce(
      (total, item) => total + item.totalPrice,
      0
    );
  }

  get isSelectionValid(): boolean {
    return this.selectedItems.length > 0;
  }
}
