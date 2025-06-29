import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  InventoryItem,
  CreateInventoryItemDto,
  UpdateInventoryItemDto,
  InventoryListResponse,
  InventoryQueryParams,
} from '../models/inventory.model';

@Injectable({
  providedIn: 'root',
})
export class InventoryService {
  private readonly baseUrl = 'http://localhost:5010/api/inventory';

  constructor(private http: HttpClient) {}

  /**
   * Get all inventory items with optional filtering and pagination
   */
  getInventoryItems(
    params?: InventoryQueryParams
  ): Observable<InventoryListResponse> {
    let httpParams = new HttpParams();

    if (params) {
      if (params.searchTerm) {
        httpParams = httpParams.set('searchTerm', params.searchTerm);
      }
      if (params.category) {
        httpParams = httpParams.set('category', params.category);
      }
      if (params.isActive !== undefined) {
        httpParams = httpParams.set('isActive', params.isActive.toString());
      }
      if (params.supplierId) {
        httpParams = httpParams.set('supplierId', params.supplierId.toString());
      }
      if (params.page) {
        httpParams = httpParams.set('page', params.page.toString());
      }
      if (params.pageSize) {
        httpParams = httpParams.set('pageSize', params.pageSize.toString());
      }
    }

    return this.http.get<InventoryListResponse>(this.baseUrl, {
      params: httpParams,
    });
  }

  /**
   * Get a specific inventory item by ID
   */
  getInventoryItem(id: number): Observable<InventoryItem> {
    return this.http.get<InventoryItem>(`${this.baseUrl}/${id}`);
  }

  /**
   * Create a new inventory item
   */
  createInventoryItem(item: CreateInventoryItemDto): Observable<InventoryItem> {
    return this.http.post<InventoryItem>(this.baseUrl, item);
  }

  /**
   * Update an existing inventory item
   */
  updateInventoryItem(
    id: number,
    item: UpdateInventoryItemDto
  ): Observable<InventoryItem> {
    return this.http.put<InventoryItem>(`${this.baseUrl}/${id}`, item);
  }

  /**
   * Delete an inventory item
   */
  deleteInventoryItem(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  /**
   * Adjust stock level for an inventory item
   */
  adjustStock(
    id: number,
    adjustment: number,
    reason?: string
  ): Observable<InventoryItem> {
    const body = { adjustment, reason };
    return this.http.post<InventoryItem>(
      `${this.baseUrl}/${id}/adjust-stock`,
      body
    );
  }

  /**
   * Get low stock items
   */
  getLowStockItems(): Observable<InventoryItem[]> {
    return this.http.get<InventoryItem[]>(`${this.baseUrl}/low-stock`);
  }

  /**
   * Get items by category
   */
  getItemsByCategory(category: string): Observable<InventoryItem[]> {
    return this.http.get<InventoryItem[]>(
      `${this.baseUrl}/category/${category}`
    );
  }

  /**
   * Get available categories
   */
  getCategories(): Observable<string[]> {
    return this.http.get<string[]>(`${this.baseUrl}/categories`);
  }
}
