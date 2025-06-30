import { Component, OnInit, OnDestroy, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  ReactiveFormsModule,
  FormBuilder,
  FormGroup,
  Validators,
} from '@angular/forms';
import { MatStepperModule, MatStepper } from '@angular/material/stepper';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatSelectModule } from '@angular/material/select';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatDividerModule } from '@angular/material/divider';
import { MatChipsModule } from '@angular/material/chips';
import { Router } from '@angular/router';
import { Subject, takeUntil } from 'rxjs';

import { OrderService } from '../../../services/order.service';
import {
  OrderStateService,
  OrderWizardState,
} from '../../../services/order-state.service';
import { InventoryService } from '../../../services/inventory.service';
import {
  CustomerInfo,
  SelectedOrderItem,
  CreateOrderDto,
  CreateOrderItemDto,
} from '../../../models/order.model';
import { InventoryItem } from '../../../models/inventory.model';
import { OrderReviewStepComponent } from '../steps/order-review-step/order-review-step.component';
@Component({
  selector: 'app-order-wizard',
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatStepperModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatCardModule,
    MatTableModule,
    MatSelectModule,
    MatCheckboxModule,
    MatDatepickerModule,
    MatNativeDateModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
    MatDividerModule,
    MatChipsModule,
    OrderReviewStepComponent,
  ],
  templateUrl: './order-wizard.component.html',
  styleUrl: './order-wizard.component.scss',
})
export class OrderWizardComponent implements OnInit, OnDestroy {
  @ViewChild('stepper') stepper!: MatStepper;

  private destroy$ = new Subject<void>();

  // Form groups for each step
  customerFormGroup!: FormGroup;
  itemSelectionFormGroup!: FormGroup;
  reviewFormGroup!: FormGroup;

  // State management
  orderState: OrderWizardState | null = null;

  // Data for item selection
  inventoryItems: InventoryItem[] = [];
  filteredItems: InventoryItem[] = [];
  isLoadingItems = false;

  // Display columns for item table
  displayedColumns: string[] = [
    'name',
    'sku',
    'price',
    'availableQuantity',
    'action',
  ];
  selectedItemsColumns: string[] = [
    'name',
    'quantity',
    'unitPrice',
    'totalPrice',
    'action',
  ];

  // UI state
  isLinearMode = true;
  currentStepIndex = 0;
  isSubmitting = false;

  constructor(
    private formBuilder: FormBuilder,
    private orderService: OrderService,
    private orderStateService: OrderStateService,
    private inventoryService: InventoryService,
    private snackBar: MatSnackBar,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.initializeForms();
    this.subscribeToOrderState();
    this.loadInventoryItems();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private initializeForms(): void {
    // Customer Information Form
    this.customerFormGroup = this.formBuilder.group({
      name: [
        '',
        [
          Validators.required,
          Validators.minLength(2),
          Validators.maxLength(100),
        ],
      ],
      email: [
        '',
        [Validators.required, Validators.email, Validators.maxLength(100)],
      ],
      phone: ['', [Validators.maxLength(20)]],
      notes: ['', [Validators.maxLength(500)]],
    });

    // Item Selection Form
    this.itemSelectionFormGroup = this.formBuilder.group({
      searchTerm: [''],
    });

    // Review Form (optional additional info)
    this.reviewFormGroup = this.formBuilder.group({
      finalNotes: ['', [Validators.maxLength(1000)]],
    });

    this.setupFormSubscriptions();
  }

  private setupFormSubscriptions(): void {
    // Listen to customer form changes and update state
    this.customerFormGroup.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe((value) => {
        if (this.customerFormGroup.valid) {
          this.orderStateService.updateCustomerInfo(value as CustomerInfo);
        }
      });

    // Listen to search term changes for filtering items
    this.itemSelectionFormGroup
      .get('searchTerm')
      ?.valueChanges.pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        this.filterItems();
      });
  }

  private subscribeToOrderState(): void {
    this.orderStateService.orderState$
      .pipe(takeUntil(this.destroy$))
      .subscribe((state) => {
        this.orderState = state;
        this.currentStepIndex = state.currentStep;

        // Update forms with state data if needed
        if (state.customerInfo && !this.customerFormGroup.dirty) {
          this.customerFormGroup.patchValue(state.customerInfo, {
            emitEvent: false,
          });
        }
      });
  }

  private filterItems(): void {
    const searchTerm =
      this.itemSelectionFormGroup.get('searchTerm')?.value?.toLowerCase() || '';
    if (searchTerm.trim() === '') {
      this.filteredItems = [...this.inventoryItems];
    } else {
      this.filteredItems = this.inventoryItems.filter(
        (item) =>
          item.name.toLowerCase().includes(searchTerm) ||
          item.description?.toLowerCase().includes(searchTerm) ||
          item.sku.toLowerCase().includes(searchTerm)
      );
    }
  }

  private loadInventoryItems(): void {
    this.isLoadingItems = true;
    this.inventoryService
      .getInventoryItems({ page: 1, pageSize: 100 })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (response) => {
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

  // Custom validator for item selection
  private atLeastOneItemValidator(
    control: any
  ): { [key: string]: boolean } | null {
    const items = control.value;
    if (!items || !Array.isArray(items) || items.length === 0) {
      return { atLeastOneItem: true };
    }
    return null;
  }

  // Stepper navigation methods
  onStepChange(event: any): void {
    this.currentStepIndex = event.selectedIndex;

    // Perform step-specific actions
    switch (this.currentStepIndex) {
      case 1:
        // Entering item selection step
        this.onEnterItemSelection();
        break;
      case 2:
        // Entering review step
        this.onEnterReview();
        break;
    }
  }

  private onEnterItemSelection(): void {
    // Logic for when user enters item selection step
    // This will be implemented in subsequent subtasks
    console.log('Entering item selection step');
  }

  private onEnterReview(): void {
    // Logic for when user enters review step
    console.log('Entering review step');
    // Order summary is automatically calculated by OrderStateService
  }

  // Form validation helpers
  isStepValid(stepIndex: number): boolean {
    switch (stepIndex) {
      case 0:
        return this.customerFormGroup.valid;
      case 1:
        return this.itemSelectionFormGroup.valid;
      case 2:
        return this.reviewFormGroup.valid;
      default:
        return false;
    }
  }

  getStepErrors(stepIndex: number): string[] {
    const errors: string[] = [];

    switch (stepIndex) {
      case 0:
        if (this.customerFormGroup.get('name')?.errors?.['required']) {
          errors.push('Customer name is required');
        }
        if (this.customerFormGroup.get('email')?.errors?.['required']) {
          errors.push('Email address is required');
        }
        if (this.customerFormGroup.get('email')?.errors?.['email']) {
          errors.push('Valid email address is required');
        }
        break;
      case 1:
        if (
          this.itemSelectionFormGroup.get('selectedItems')?.errors?.[
            'atLeastOneItem'
          ]
        ) {
          errors.push('At least one item must be selected');
        }
        break;
    }

    return errors;
  }

  // Navigation methods
  goToNextStep(): void {
    const nextStep = this.currentStepIndex + 1;
    if (nextStep <= 2 && this.orderStateService.canProceedToStep(nextStep)) {
      this.orderStateService.setCurrentStep(nextStep);
      this.stepper.next();
    }
  }

  goToPreviousStep(): void {
    const previousStep = this.currentStepIndex - 1;
    if (previousStep >= 0) {
      this.orderStateService.setCurrentStep(previousStep);
      this.stepper.previous();
    }
  }

  goToStep(index: number): void {
    if (this.orderStateService.canProceedToStep(index)) {
      this.orderStateService.setCurrentStep(index);
      this.stepper.selectedIndex = index;
    }
  }

  // Reset and navigation
  resetWizard(): void {
    this.customerFormGroup.reset();
    this.itemSelectionFormGroup.reset();
    this.reviewFormGroup.reset();

    this.orderStateService.resetState();

    this.stepper.reset();
    this.currentStepIndex = 0;
  }

  onCancel(): void {
    if (
      confirm('Are you sure you want to cancel? All entered data will be lost.')
    ) {
      this.resetWizard();
      this.router.navigate(['/dashboard']);
    }
  }

  // Final submission (to be implemented in later subtasks)
  onSubmitOrder(): void {
    if (!this.orderStateService.isReadyForSubmission()) {
      this.snackBar.open('Please complete all required fields', 'Close', {
        duration: 3000,
        panelClass: ['error-snackbar'],
      });
      return;
    }

    this.isSubmitting = true;

    // Create order DTO
    if (!this.orderState) {
      this.snackBar.open('Order state not available', 'Close', {
        duration: 3000,
        panelClass: ['error-snackbar'],
      });
      return;
    }

    const customerInfo = this.orderState.customerInfo;
    const selectedItems = this.orderState.selectedItems;

    if (!customerInfo || !selectedItems) {
      this.snackBar.open('Missing required order information', 'Close', {
        duration: 3000,
        panelClass: ['error-snackbar'],
      });
      return;
    }

    const orderDto: CreateOrderDto = {
      customerName: customerInfo.name,
      customerEmail: customerInfo.email,
      customerPhone: customerInfo.phone || '',
      notes: customerInfo.notes || '',
      orderItems: selectedItems.map((item: SelectedOrderItem) => ({
        inventoryItemId: item.inventoryItemId,
        quantity: item.selectedQuantity,
      })),
    };

    this.orderService
      .createOrder(orderDto)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (order) => {
          this.snackBar.open('Order created successfully!', 'Close', {
            duration: 3000,
            panelClass: ['success-snackbar'],
          });
          this.router.navigate(['/orders', order.id]);
        },
        error: (error) => {
          console.error('Error creating order:', error);
          this.snackBar.open(
            'Error creating order. Please try again.',
            'Close',
            {
              duration: 5000,
              panelClass: ['error-snackbar'],
            }
          );
          this.isSubmitting = false;
        },
      });
  }

  isAllStepsValid(): boolean {
    return this.isStepValid(0) && this.isStepValid(1) && this.isStepValid(2);
  }

  // Utility methods
  formatCurrency(amount: number): string {
    return new Intl.NumberFormat('en-US', {
      style: 'currency',
      currency: 'USD',
    }).format(amount);
  }

  get canProceedToNext(): boolean {
    return this.isStepValid(this.currentStepIndex);
  }

  // Mock methods for testing (to be replaced in later subtasks)
  addMockItem(): void {
    const mockItem: SelectedOrderItem = {
      inventoryItemId: 1,
      inventoryItemName: 'Sample Product',
      availableQuantity: 10,
      unitPrice: 99.99,
      selectedQuantity: 1,
      totalPrice: 99.99,
    };

    this.orderStateService.addItem(mockItem);
  }

  removeSelectedItem(item: SelectedOrderItem): void {
    this.orderStateService.removeItem(item.inventoryItemId);
  }

  // Helper methods
  get canSubmitOrder(): boolean {
    return this.orderStateService.isReadyForSubmission();
  }

  get isStepValidGetter(): boolean {
    if (!this.orderState) return false;

    switch (this.currentStepIndex) {
      case 0:
        return this.orderState.steps[0].isValid;
      case 1:
        return this.orderState.steps[1].isValid;
      case 2:
        return this.orderState.steps[2].isValid;
      default:
        return false;
    }
  }

  get isFirstStep(): boolean {
    return this.currentStepIndex === 0;
  }

  get isLastStep(): boolean {
    return this.currentStepIndex === 2;
  }

  get canProceedFromCurrentStep(): boolean {
    return this.orderStateService.canProceedToStep(this.currentStepIndex + 1);
  }
}
