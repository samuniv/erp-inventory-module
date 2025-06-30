import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { delay, map } from 'rxjs/operators';
import {
  Order,
  CreateOrderDto,
  UpdateOrderDto,
  OrderQueryParams,
  OrderPagedResponse,
  OrderStatus,
} from '../models/order.model';
import { environment } from '../../environments/environment';

@Injectable({
  providedIn: 'root',
})
export class OrderService {
  private readonly apiUrl = `${environment.apiUrl}/api/orders`;

  constructor(private http: HttpClient) {}

  /**
   * Get all orders with optional filtering and pagination
   */
  getOrders(params?: OrderQueryParams): Observable<OrderPagedResponse> {
    // For development, return mock data
    if (!environment.production) {
      return this.getMockOrders(params);
    }

    let httpParams = new HttpParams();
    if (params) {
      if (params.page)
        httpParams = httpParams.set('page', params.page.toString());
      if (params.pageSize)
        httpParams = httpParams.set('pageSize', params.pageSize.toString());
      if (params.searchTerm)
        httpParams = httpParams.set('searchTerm', params.searchTerm);
      if (params.status) httpParams = httpParams.set('status', params.status);
      if (params.sortBy) httpParams = httpParams.set('sortBy', params.sortBy);
      if (params.sortDirection)
        httpParams = httpParams.set('sortDirection', params.sortDirection);
      if (params.dateFrom)
        httpParams = httpParams.set('dateFrom', params.dateFrom.toISOString());
      if (params.dateTo)
        httpParams = httpParams.set('dateTo', params.dateTo.toISOString());
    }

    return this.http.get<OrderPagedResponse>(this.apiUrl, {
      params: httpParams,
    });
  }

  /**
   * Get a specific order by ID
   */
  getOrderById(id: number): Observable<Order> {
    // For development, return mock data
    if (!environment.production) {
      return this.getMockOrderById(id);
    }

    return this.http.get<Order>(`${this.apiUrl}/${id}`);
  }

  /**
   * Create a new order
   */
  createOrder(order: CreateOrderDto): Observable<Order> {
    // For development, return mock data
    if (!environment.production) {
      return this.createMockOrder(order);
    }

    return this.http.post<Order>(this.apiUrl, order);
  }

  /**
   * Update an existing order
   */
  updateOrder(id: number, order: UpdateOrderDto): Observable<Order> {
    // For development, return mock data
    if (!environment.production) {
      return this.updateMockOrder(id, order);
    }

    return this.http.put<Order>(`${this.apiUrl}/${id}`, order);
  }

  /**
   * Delete an order
   */
  deleteOrder(id: number): Observable<void> {
    // For development, return mock data
    if (!environment.production) {
      return this.deleteMockOrder(id);
    }

    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }

  /**
   * Update order status
   */
  updateOrderStatus(id: number, status: OrderStatus): Observable<Order> {
    return this.updateOrder(id, { status });
  }

  // Mock data methods for development
  private getMockOrders(
    params?: OrderQueryParams
  ): Observable<OrderPagedResponse> {
    const mockOrders: Order[] = [
      {
        id: 1,
        orderNumber: 'ORD-2024-001',
        customerName: 'John Doe',
        customerEmail: 'john.doe@example.com',
        customerPhone: '+1-555-0123',
        orderDate: new Date('2024-01-15'),
        totalAmount: 1250.0,
        status: OrderStatus.Processing,
        notes: 'Urgent delivery required',
        orderItems: [
          {
            id: 1,
            orderId: 1,
            inventoryItemId: 1,
            inventoryItemName: 'Laptop Computer',
            quantity: 1,
            unitPrice: 1000.0,
            totalPrice: 1000.0,
          },
          {
            id: 2,
            orderId: 1,
            inventoryItemId: 2,
            inventoryItemName: 'Wireless Mouse',
            quantity: 5,
            unitPrice: 50.0,
            totalPrice: 250.0,
          },
        ],
      },
      {
        id: 2,
        orderNumber: 'ORD-2024-002',
        customerName: 'Jane Smith',
        customerEmail: 'jane.smith@example.com',
        customerPhone: '+1-555-0456',
        orderDate: new Date('2024-01-16'),
        totalAmount: 750.5,
        status: OrderStatus.Shipped,
        notes: 'Gift wrapping requested',
        orderItems: [
          {
            id: 3,
            orderId: 2,
            inventoryItemId: 3,
            inventoryItemName: 'Keyboard',
            quantity: 2,
            unitPrice: 150.0,
            totalPrice: 300.0,
          },
          {
            id: 4,
            orderId: 2,
            inventoryItemId: 4,
            inventoryItemName: 'Monitor',
            quantity: 1,
            unitPrice: 450.5,
            totalPrice: 450.5,
          },
        ],
      },
      {
        id: 3,
        orderNumber: 'ORD-2024-003',
        customerName: 'Bob Johnson',
        customerEmail: 'bob.johnson@example.com',
        orderDate: new Date('2024-01-17'),
        totalAmount: 299.99,
        status: OrderStatus.Delivered,
        orderItems: [
          {
            id: 5,
            orderId: 3,
            inventoryItemId: 5,
            inventoryItemName: 'USB Cable',
            quantity: 10,
            unitPrice: 29.99,
            totalPrice: 299.9,
          },
        ],
      },
      {
        id: 4,
        orderNumber: 'ORD-2024-004',
        customerName: 'Alice Brown',
        customerEmail: 'alice.brown@example.com',
        customerPhone: '+1-555-0789',
        orderDate: new Date('2024-01-18'),
        totalAmount: 2100.0,
        status: OrderStatus.Pending,
        notes: 'Corporate purchase order',
        orderItems: [
          {
            id: 6,
            orderId: 4,
            inventoryItemId: 1,
            inventoryItemName: 'Laptop Computer',
            quantity: 2,
            unitPrice: 1000.0,
            totalPrice: 2000.0,
          },
          {
            id: 7,
            orderId: 4,
            inventoryItemId: 3,
            inventoryItemName: 'Keyboard',
            quantity: 2,
            unitPrice: 50.0,
            totalPrice: 100.0,
          },
        ],
      },
      {
        id: 5,
        orderNumber: 'ORD-2024-005',
        customerName: 'Charlie Wilson',
        customerEmail: 'charlie.wilson@example.com',
        orderDate: new Date('2024-01-19'),
        totalAmount: 899.95,
        status: OrderStatus.Cancelled,
        notes: 'Customer requested cancellation',
        orderItems: [
          {
            id: 8,
            orderId: 5,
            inventoryItemId: 4,
            inventoryItemName: 'Monitor',
            quantity: 2,
            unitPrice: 449.99,
            totalPrice: 899.98,
          },
        ],
      },
    ];

    // Apply filtering
    let filteredOrders = [...mockOrders];

    if (params?.searchTerm) {
      const searchLower = params.searchTerm.toLowerCase();
      filteredOrders = filteredOrders.filter(
        (order) =>
          order.customerName.toLowerCase().includes(searchLower) ||
          order.customerEmail.toLowerCase().includes(searchLower) ||
          order.orderNumber.toLowerCase().includes(searchLower)
      );
    }

    if (params?.status) {
      filteredOrders = filteredOrders.filter(
        (order) => order.status === params.status
      );
    }

    if (params?.dateFrom) {
      filteredOrders = filteredOrders.filter(
        (order) => new Date(order.orderDate) >= new Date(params.dateFrom!)
      );
    }

    if (params?.dateTo) {
      filteredOrders = filteredOrders.filter(
        (order) => new Date(order.orderDate) <= new Date(params.dateTo!)
      );
    }

    // Apply sorting
    if (params?.sortBy) {
      filteredOrders.sort((a, b) => {
        const direction = params.sortDirection === 'desc' ? -1 : 1;
        const aValue = this.getPropertyValue(a, params.sortBy!);
        const bValue = this.getPropertyValue(b, params.sortBy!);

        if (aValue < bValue) return -1 * direction;
        if (aValue > bValue) return 1 * direction;
        return 0;
      });
    }

    // Apply pagination
    const page = params?.page || 1;
    const pageSize = params?.pageSize || 10;
    const startIndex = (page - 1) * pageSize;
    const endIndex = startIndex + pageSize;
    const paginatedOrders = filteredOrders.slice(startIndex, endIndex);

    const response: OrderPagedResponse = {
      items: paginatedOrders,
      totalCount: filteredOrders.length,
      page: page,
      pageSize: pageSize,
      totalPages: Math.ceil(filteredOrders.length / pageSize),
    };

    return of(response).pipe(delay(500)); // Simulate network delay
  }

  private getMockOrderById(id: number): Observable<Order> {
    return this.getMockOrders().pipe(
      map((response) => {
        const order = response.items.find((o) => o.id === id);
        if (!order) {
          throw new Error(`Order with ID ${id} not found`);
        }
        return order;
      })
    );
  }

  private createMockOrder(orderDto: CreateOrderDto): Observable<Order> {
    const newOrder: Order = {
      id: Math.floor(Math.random() * 1000) + 100,
      orderNumber: `ORD-${new Date().getFullYear()}-${String(
        Math.floor(Math.random() * 1000)
      ).padStart(3, '0')}`,
      customerName: orderDto.customerName,
      customerEmail: orderDto.customerEmail,
      customerPhone: orderDto.customerPhone,
      orderDate: new Date(),
      totalAmount: orderDto.orderItems.reduce(
        (sum, item) => sum + item.quantity * 50,
        0
      ), // Mock calculation
      status: OrderStatus.Pending,
      notes: orderDto.notes,
      orderItems: orderDto.orderItems.map((item, index) => ({
        id: index + 1,
        inventoryItemId: item.inventoryItemId,
        inventoryItemName: `Mock Item ${item.inventoryItemId}`,
        quantity: item.quantity,
        unitPrice: 50, // Mock price
        totalPrice: item.quantity * 50,
      })),
    };

    return of(newOrder).pipe(delay(1000));
  }

  private updateMockOrder(
    id: number,
    orderDto: UpdateOrderDto
  ): Observable<Order> {
    return this.getMockOrderById(id).pipe(
      map((order) => ({
        ...order,
        ...orderDto,
        id: order.id, // Ensure ID doesn't change
      })),
      delay(500)
    );
  }

  private deleteMockOrder(id: number): Observable<void> {
    return of(void 0).pipe(delay(500));
  }

  private getPropertyValue(obj: any, propertyPath: string): any {
    return propertyPath.split('.').reduce((o, p) => o && o[p], obj);
  }
}
