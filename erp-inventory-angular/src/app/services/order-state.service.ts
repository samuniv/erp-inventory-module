import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable } from 'rxjs';
import {
  OrderWizardData,
  CustomerInfo,
  SelectedOrderItem,
} from '../models/order.model';

export interface OrderStep {
  isCompleted: boolean;
  isValid: boolean;
}

export interface OrderWizardState {
  customerInfo: CustomerInfo | null;
  selectedItems: SelectedOrderItem[];
  currentStep: number;
  steps: OrderStep[];
  totalAmount: number;
  isSubmitting: boolean;
}

@Injectable({
  providedIn: 'root',
})
export class OrderStateService {
  private initialState: OrderWizardState = {
    customerInfo: null,
    selectedItems: [],
    currentStep: 0,
    steps: [
      { isCompleted: false, isValid: false }, // Customer Info
      { isCompleted: false, isValid: false }, // Item Selection
      { isCompleted: false, isValid: false }, // Review & Submit
    ],
    totalAmount: 0,
    isSubmitting: false,
  };

  private orderStateSubject = new BehaviorSubject<OrderWizardState>(
    this.initialState
  );
  public orderState$: Observable<OrderWizardState> =
    this.orderStateSubject.asObservable();

  constructor() {}

  // Get current state
  getCurrentState(): OrderWizardState {
    return this.orderStateSubject.value;
  }

  // Update customer information
  updateCustomerInfo(customerInfo: CustomerInfo): void {
    const currentState = this.getCurrentState();
    const updatedState: OrderWizardState = {
      ...currentState,
      customerInfo,
      steps: [
        {
          ...currentState.steps[0],
          isCompleted: true,
          isValid: this.isCustomerInfoValid(customerInfo),
        },
        currentState.steps[1],
        currentState.steps[2],
      ],
    };
    this.orderStateSubject.next(updatedState);
  }

  // Update selected items
  updateSelectedItems(items: SelectedOrderItem[]): void {
    const currentState = this.getCurrentState();
    const totalAmount = this.calculateTotalAmount(items);
    const updatedState: OrderWizardState = {
      ...currentState,
      selectedItems: items,
      totalAmount,
      steps: [
        currentState.steps[0],
        {
          ...currentState.steps[1],
          isCompleted: items.length > 0,
          isValid: items.length > 0,
        },
        currentState.steps[2],
      ],
    };
    this.orderStateSubject.next(updatedState);
  }

  // Add item to selection
  addItem(item: SelectedOrderItem): void {
    const currentState = this.getCurrentState();
    const existingItemIndex = currentState.selectedItems.findIndex(
      (i) => i.inventoryItemId === item.inventoryItemId
    );

    let updatedItems: SelectedOrderItem[];
    if (existingItemIndex >= 0) {
      // Update existing item quantity
      updatedItems = [...currentState.selectedItems];
      updatedItems[existingItemIndex] = {
        ...updatedItems[existingItemIndex],
        selectedQuantity:
          updatedItems[existingItemIndex].selectedQuantity +
          item.selectedQuantity,
        totalPrice:
          (updatedItems[existingItemIndex].selectedQuantity +
            item.selectedQuantity) *
          updatedItems[existingItemIndex].unitPrice,
      };
    } else {
      // Add new item
      updatedItems = [...currentState.selectedItems, item];
    }

    this.updateSelectedItems(updatedItems);
  }

  // Remove item from selection
  removeItem(inventoryItemId: number): void {
    const currentState = this.getCurrentState();
    const updatedItems = currentState.selectedItems.filter(
      (item) => item.inventoryItemId !== inventoryItemId
    );
    this.updateSelectedItems(updatedItems);
  }

  // Update item quantity
  updateItemQuantity(inventoryItemId: number, quantity: number): void {
    const currentState = this.getCurrentState();
    if (quantity <= 0) {
      this.removeItem(inventoryItemId);
      return;
    }

    const updatedItems = currentState.selectedItems.map((item) =>
      item.inventoryItemId === inventoryItemId
        ? {
            ...item,
            selectedQuantity: quantity,
            totalPrice: quantity * item.unitPrice,
          }
        : item
    );
    this.updateSelectedItems(updatedItems);
  }

  // Set current step
  setCurrentStep(step: number): void {
    const currentState = this.getCurrentState();
    if (step >= 0 && step < currentState.steps.length) {
      const updatedState: OrderWizardState = {
        ...currentState,
        currentStep: step,
      };
      this.orderStateSubject.next(updatedState);
    }
  }

  // Complete step
  completeStep(stepIndex: number): void {
    const currentState = this.getCurrentState();
    if (stepIndex >= 0 && stepIndex < currentState.steps.length) {
      const updatedSteps = [...currentState.steps];
      updatedSteps[stepIndex] = {
        ...updatedSteps[stepIndex],
        isCompleted: true,
      };

      const updatedState: OrderWizardState = {
        ...currentState,
        steps: updatedSteps,
      };
      this.orderStateSubject.next(updatedState);
    }
  }

  // Set submission state
  setSubmissionState(isSubmitting: boolean): void {
    const currentState = this.getCurrentState();
    const updatedState: OrderWizardState = {
      ...currentState,
      isSubmitting,
    };
    this.orderStateSubject.next(updatedState);
  }

  // Get order data for submission
  getOrderForSubmission(): Partial<OrderWizardData> | null {
    const state = this.getCurrentState();
    if (!state.customerInfo || state.selectedItems.length === 0) {
      return null;
    }

    return {
      customerInfo: state.customerInfo,
      selectedItems: state.selectedItems,
    };
  }

  // Reset state
  resetState(): void {
    this.orderStateSubject.next(this.initialState);
  }

  // Alias for resetState - semantic naming for order completion
  resetOrder(): void {
    this.resetState();
  }

  // Check if wizard can proceed to next step
  canProceedToStep(stepIndex: number): boolean {
    const currentState = this.getCurrentState();

    switch (stepIndex) {
      case 0: // Customer Info step
        return true; // Always can access first step
      case 1: // Item Selection step
        return currentState.steps[0].isValid; // Need valid customer info
      case 2: // Review step
        return currentState.steps[0].isValid && currentState.steps[1].isValid; // Need both previous steps
      default:
        return false;
    }
  }

  // Check if order is ready for submission
  isReadyForSubmission(): boolean {
    const state = this.getCurrentState();
    return (
      state.steps[0].isValid &&
      state.steps[1].isValid &&
      state.selectedItems.length > 0 &&
      state.customerInfo !== null
    );
  }

  // Private helper methods
  private isCustomerInfoValid(customerInfo: CustomerInfo): boolean {
    return !!(
      customerInfo.name &&
      customerInfo.email &&
      this.isValidEmail(customerInfo.email)
    );
  }

  private isValidEmail(email: string): boolean {
    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    return emailRegex.test(email);
  }

  private calculateTotalAmount(items: SelectedOrderItem[]): number {
    return items.reduce(
      (total, item) => total + item.unitPrice * item.selectedQuantity,
      0
    );
  }
}
