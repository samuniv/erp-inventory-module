export interface InventoryItem {
  id: number;
  name: string;
  description?: string;
  sku: string;
  price: number;
  stockLevel: number;
  reorderThreshold: number;
  category: string;
  supplierId?: number;
  supplierName?: string;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
  createdBy?: string;
  updatedBy?: string;
}

export interface CreateInventoryItemDto {
  name: string;
  description?: string;
  sku: string;
  price: number;
  stockLevel: number;
  reorderThreshold: number;
  category: string;
  supplierId?: number;
}

export interface UpdateInventoryItemDto {
  name: string;
  description?: string;
  sku: string;
  price: number;
  stockLevel: number;
  reorderThreshold: number;
  category: string;
  supplierId?: number;
  isActive: boolean;
}

export interface InventoryListResponse {
  items: InventoryItem[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface InventoryQueryParams {
  searchTerm?: string;
  category?: string;
  isActive?: boolean;
  supplierId?: number;
  page?: number;
  pageSize?: number;
}
