import { Component, Input, OnInit, OnDestroy, inject } from '@angular/core';
import { FormGroup, ReactiveFormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatDividerModule } from '@angular/material/divider';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Router } from '@angular/router';
import { Subject, takeUntil, finalize } from 'rxjs';
import {
  OrderStateService,
  OrderWizardState,
} from '../../../../services/order-state.service';
import { OrderService } from '../../../../services/order.service';
import {
  CustomerInfo,
  SelectedOrderItem,
  CreateOrderDto,
  CreateOrderItemDto,
} from '../../../../models/order.model';

@Component({
  selector: 'app-order-review-step',
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatCardModule,
    MatIconModule,
    MatButtonModule,
    MatDividerModule,
    MatChipsModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './order-review-step.component.html',
  styleUrl: './order-review-step.component.scss',
})
export class OrderReviewStepComponent implements OnInit, OnDestroy {
  @Input() formGroup!: FormGroup;

  private destroy$ = new Subject<void>();
  private snackBar = inject(MatSnackBar);
  private router = inject(Router);

  orderState!: OrderWizardState;
  isSubmitting = false;

  constructor(
    private orderStateService: OrderStateService,
    private orderService: OrderService
  ) {}

  ngOnInit(): void {
    this.subscribeToOrderState();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private subscribeToOrderState(): void {
    this.orderStateService.orderState$
      .pipe(takeUntil(this.destroy$))
      .subscribe((state) => {
        this.orderState = state;
        this.isSubmitting = state.isSubmitting;
      });
  }

  // Submit Order Logic
  async submitOrder(): Promise<void> {
    if (!this.canSubmitOrder) {
      this.showErrorMessage(
        'Please complete all required fields before submitting'
      );
      return;
    }

    this.orderStateService.setSubmissionState(true);

    try {
      const orderDto = this.prepareOrderData();

      const result = await this.orderService
        .createOrder(orderDto)
        .pipe(finalize(() => this.orderStateService.setSubmissionState(false)))
        .toPromise();

      if (result) {
        this.showSuccessMessage('Order submitted successfully!');
        this.resetOrderState();
        await this.navigateToOrdersList();
      }
    } catch (error) {
      this.handleSubmissionError(error);
    }
  }

  // Computed properties
  get canSubmitOrder(): boolean {
    return this.orderStateService.isReadyForSubmission() && !this.isSubmitting;
  }

  private prepareOrderData(): CreateOrderDto {
    const customerInfo = this.customerInfo;
    const selectedItems = this.selectedItems;

    if (!customerInfo) {
      throw new Error('Customer information is missing');
    }

    if (!selectedItems || selectedItems.length === 0) {
      throw new Error('No items selected');
    }

    const orderItems: CreateOrderItemDto[] = selectedItems.map((item) => ({
      inventoryItemId: item.inventoryItemId,
      quantity: item.selectedQuantity,
    }));

    return {
      customerName: customerInfo.name,
      customerEmail: customerInfo.email,
      customerPhone: customerInfo.phone || '',
      notes: customerInfo.notes || '',
      orderItems,
    };
  }

  private showSuccessMessage(message: string): void {
    this.snackBar.open(message, 'Close', {
      duration: 5000,
      panelClass: ['success-snackbar'],
    });
  }

  private showErrorMessage(message: string): void {
    this.snackBar.open(message, 'Close', {
      duration: 8000,
      panelClass: ['error-snackbar'],
    });
  }

  private handleSubmissionError(error: any): void {
    console.error('Order submission failed:', error);

    let message = 'Failed to submit order. Please try again.';

    if (error?.error?.message) {
      message = error.error.message;
    } else if (error?.message) {
      message = error.message;
    } else if (typeof error === 'string') {
      message = error;
    }

    this.showErrorMessage(message);
  }

  private resetOrderState(): void {
    this.orderStateService.resetOrder();
  }

  private async navigateToOrdersList(): Promise<void> {
    await this.router.navigate(['/orders']);
  }

  // Helper methods
  formatCurrency(amount: number): string {
    return new Intl.NumberFormat('en-US', {
      style: 'currency',
      currency: 'USD',
    }).format(amount);
  }

  get customerInfo(): CustomerInfo | null {
    return this.orderState?.customerInfo || null;
  }

  get selectedItems(): SelectedOrderItem[] {
    return this.orderState?.selectedItems || [];
  }

  get totalAmount(): number {
    return this.orderState?.totalAmount || 0;
  }

  get taxAmount(): number {
    return this.totalAmount * 0.08; // 8% tax
  }

  get grandTotal(): number {
    return this.totalAmount + this.taxAmount;
  }

  get totalItemCount(): number {
    return this.selectedItems.reduce(
      (total, item) => total + item.selectedQuantity,
      0
    );
  }

  get isOrderValid(): boolean {
    return this.orderStateService.isReadyForSubmission();
  }

  // Navigation helpers (for wizard integration)
  editCustomerInfo(): void {
    this.orderStateService.setCurrentStep(0);
  }

  editItemSelection(): void {
    this.orderStateService.setCurrentStep(1);
  }
}
