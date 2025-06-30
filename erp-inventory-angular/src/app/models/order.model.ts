// Order DTOs and Models
export interface Order {
  id: number;
  orderNumber: string;
  customerId?: number;
  customerName: string;
  customerEmail: string;
  customerPhone?: string;
  orderDate: Date;
  totalAmount: number;
  status: OrderStatus;
  notes?: string;
  orderItems: OrderItem[];
}

export interface OrderItem {
  id?: number;
  orderId?: number;
  inventoryItemId: number;
  inventoryItemName: string;
  quantity: number;
  unitPrice: number;
  totalPrice: number;
}

export interface CreateOrderDto {
  customerName: string;
  customerEmail: string;
  customerPhone?: string;
  notes?: string;
  orderItems: CreateOrderItemDto[];
}

export interface CreateOrderItemDto {
  inventoryItemId: number;
  quantity: number;
}

export interface UpdateOrderDto {
  customerName?: string;
  customerEmail?: string;
  customerPhone?: string;
  notes?: string;
  status?: OrderStatus;
}

export interface OrderQueryParams {
  page?: number;
  pageSize?: number;
  searchTerm?: string;
  status?: OrderStatus;
  sortBy?: string;
  sortDirection?: 'asc' | 'desc';
  dateFrom?: Date;
  dateTo?: Date;
}

export interface OrderPagedResponse {
  items: Order[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export enum OrderStatus {
  Pending = 'Pending',
  Processing = 'Processing',
  Shipped = 'Shipped',
  Delivered = 'Delivered',
  Cancelled = 'Cancelled',
}

// Order Wizard specific models
export interface OrderWizardData {
  customerInfo: CustomerInfo;
  selectedItems: SelectedOrderItem[];
  orderSummary: OrderSummary;
}

export interface CustomerInfo {
  name: string;
  email: string;
  phone: string;
  notes: string;
}

export interface SelectedOrderItem {
  inventoryItemId: number;
  inventoryItemName: string;
  availableQuantity: number;
  unitPrice: number;
  selectedQuantity: number;
  totalPrice: number;
}

export interface OrderSummary {
  subtotal: number;
  tax: number;
  total: number;
  itemCount: number;
}

// Form validation interfaces
export interface CustomerFormData {
  name: string;
  email: string;
  phone: string;
  notes: string;
}

export interface ItemSelectionFormData {
  selectedItems: SelectedOrderItem[];
}

// Stepper configuration
export interface StepperConfig {
  currentStep: number;
  totalSteps: number;
  isLinear: boolean;
  allowStepSelection: boolean;
}
