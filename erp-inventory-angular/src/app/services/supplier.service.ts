import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, of, throwError } from 'rxjs';
import { map, catchError, delay } from 'rxjs/operators';
import {
  Supplier,
  CreateSupplierDto,
  UpdateSupplierDto,
  SupplierQueryParams,
  SupplierPagedResponse,
} from '../models/supplier.model';

@Injectable({
  providedIn: 'root',
})
export class SupplierService {
  private readonly apiUrl = '/api/suppliers';

  // Mock data for development (replace with real API calls when backend is available)
  private mockSuppliers: Supplier[] = [
    {
      id: 1,
      name: 'Global Electronics Supply Co.',
      contactPerson: 'John Smith',
      email: 'john.smith@global-electronics.com',
      phone: '+1-555-0123',
      address: '123 Industrial Blvd, Tech City, TC 12345',
      isActive: true,
      createdAt: new Date('2024-01-15'),
      updatedAt: new Date('2024-01-15'),
    },
    {
      id: 2,
      name: 'TechComponents Ltd.',
      contactPerson: 'Sarah Johnson',
      email: 'sarah.johnson@techcomponents.co.uk',
      phone: '+44-20-7946-0958',
      address: '456 Innovation Street, London, UK SW1A 1AA',
      isActive: true,
      createdAt: new Date('2024-02-01'),
      updatedAt: new Date('2024-02-01'),
    },
    {
      id: 3,
      name: 'European Parts Distribution',
      contactPerson: 'Marco Rossi',
      email: 'marco.rossi@europarts.eu',
      phone: '+39-06-1234-5678',
      address: '789 Commerce Way, Milan, Italy 20100',
      isActive: true,
      createdAt: new Date('2024-02-15'),
      updatedAt: new Date('2024-02-15'),
    },
    {
      id: 4,
      name: 'Asian Components Hub',
      contactPerson: 'Li Wei',
      email: 'li.wei@asianhub.com',
      phone: '+86-21-6789-0123',
      address: '321 Manufacturing District, Shanghai, China 200000',
      isActive: false,
      createdAt: new Date('2024-03-01'),
      updatedAt: new Date('2024-03-10'),
    },
    {
      id: 5,
      name: 'North American Supplies Inc.',
      contactPerson: 'Emily Davis',
      email: 'emily.davis@nasupplies.com',
      phone: '+1-555-0456',
      address: '654 Logistics Lane, Chicago, IL 60601',
      isActive: true,
      createdAt: new Date('2024-03-15'),
      updatedAt: new Date('2024-03-15'),
    },
  ];

  constructor(private http: HttpClient) {}

  /**
   * Get suppliers with optional filtering and pagination
   */
  getSuppliers(
    params?: SupplierQueryParams
  ): Observable<SupplierPagedResponse> {
    // For now, use mock data. Replace with actual HTTP call when backend is ready
    return this.getMockSuppliers(params);

    // Real implementation would be:
    // let httpParams = new HttpParams();
    // if (params?.page) httpParams = httpParams.set('page', params.page.toString());
    // if (params?.pageSize) httpParams = httpParams.set('pageSize', params.pageSize.toString());
    // if (params?.searchTerm) httpParams = httpParams.set('searchTerm', params.searchTerm);
    // if (params?.isActive !== undefined) httpParams = httpParams.set('isActive', params.isActive.toString());

    // return this.http.get<SupplierPagedResponse>(this.apiUrl, { params: httpParams })
    //   .pipe(
    //     catchError(this.handleError)
    //   );
  }

  /**
   * Get a specific supplier by ID
   */
  getSupplier(id: number): Observable<Supplier> {
    // Mock implementation
    return this.getMockSupplier(id);

    // Real implementation:
    // return this.http.get<Supplier>(`${this.apiUrl}/${id}`)
    //   .pipe(
    //     catchError(this.handleError)
    //   );
  }

  /**
   * Create a new supplier
   */
  createSupplier(supplier: CreateSupplierDto): Observable<Supplier> {
    // Mock implementation
    return this.createMockSupplier(supplier);

    // Real implementation:
    // return this.http.post<Supplier>(this.apiUrl, supplier)
    //   .pipe(
    //     catchError(this.handleError)
    //   );
  }

  /**
   * Update an existing supplier
   */
  updateSupplier(
    id: number,
    supplier: UpdateSupplierDto
  ): Observable<Supplier> {
    // Mock implementation
    return this.updateMockSupplier(id, supplier);

    // Real implementation:
    // return this.http.put<Supplier>(`${this.apiUrl}/${id}`, supplier)
    //   .pipe(
    //     catchError(this.handleError)
    //   );
  }

  /**
   * Delete a supplier
   */
  deleteSupplier(id: number): Observable<void> {
    // Mock implementation
    return this.deleteMockSupplier(id);

    // Real implementation:
    // return this.http.delete<void>(`${this.apiUrl}/${id}`)
    //   .pipe(
    //     catchError(this.handleError)
    //   );
  }

  /**
   * Get all active suppliers (simplified method for dropdowns)
   */
  getActiveSuppliers(): Observable<Supplier[]> {
    return this.getSuppliers({ isActive: true }).pipe(
      map((response) => response.items)
    );
  }

  // Mock implementations for development

  private getMockSuppliers(
    params?: SupplierQueryParams
  ): Observable<SupplierPagedResponse> {
    return of(null).pipe(
      delay(300), // Simulate network delay
      map(() => {
        let filteredSuppliers = [...this.mockSuppliers];

        // Apply search filter
        if (params?.searchTerm) {
          const searchTerm = params.searchTerm.toLowerCase();
          filteredSuppliers = filteredSuppliers.filter(
            (supplier) =>
              supplier.name.toLowerCase().includes(searchTerm) ||
              supplier.contactPerson.toLowerCase().includes(searchTerm) ||
              supplier.email.toLowerCase().includes(searchTerm)
          );
        }

        // Apply active filter
        if (params?.isActive !== undefined) {
          filteredSuppliers = filteredSuppliers.filter(
            (supplier) => supplier.isActive === params.isActive
          );
        }

        // Calculate pagination
        const page = params?.page || 1;
        const pageSize = params?.pageSize || 10;
        const startIndex = (page - 1) * pageSize;
        const endIndex = startIndex + pageSize;
        const paginatedSuppliers = filteredSuppliers.slice(
          startIndex,
          endIndex
        );

        return {
          items: paginatedSuppliers,
          totalCount: filteredSuppliers.length,
          page: page,
          pageSize: pageSize,
        };
      })
    );
  }

  private getMockSupplier(id: number): Observable<Supplier> {
    return of(null).pipe(
      delay(200),
      map(() => {
        const supplier = this.mockSuppliers.find((s) => s.id === id);
        if (!supplier) {
          throw new Error(`Supplier with ID ${id} not found`);
        }
        return supplier;
      }),
      catchError(this.handleError)
    );
  }

  private createMockSupplier(
    supplierDto: CreateSupplierDto
  ): Observable<Supplier> {
    return of(null).pipe(
      delay(500),
      map(() => {
        const newSupplier: Supplier = {
          id: Math.max(...this.mockSuppliers.map((s) => s.id)) + 1,
          name: supplierDto.name,
          contactPerson: supplierDto.contactPerson,
          email: supplierDto.email,
          phone: supplierDto.phone,
          address: supplierDto.address,
          isActive: supplierDto.isActive ?? true,
          createdAt: new Date(),
          updatedAt: new Date(),
        };

        this.mockSuppliers.push(newSupplier);
        return newSupplier;
      })
    );
  }

  private updateMockSupplier(
    id: number,
    supplierDto: UpdateSupplierDto
  ): Observable<Supplier> {
    return of(null).pipe(
      delay(400),
      map(() => {
        const supplierIndex = this.mockSuppliers.findIndex((s) => s.id === id);
        if (supplierIndex === -1) {
          throw new Error(`Supplier with ID ${id} not found`);
        }

        const updatedSupplier: Supplier = {
          ...this.mockSuppliers[supplierIndex],
          ...supplierDto,
          updatedAt: new Date(),
        };

        this.mockSuppliers[supplierIndex] = updatedSupplier;
        return updatedSupplier;
      }),
      catchError(this.handleError)
    );
  }

  private deleteMockSupplier(id: number): Observable<void> {
    return of(null).pipe(
      delay(300),
      map(() => {
        const supplierIndex = this.mockSuppliers.findIndex((s) => s.id === id);
        if (supplierIndex === -1) {
          throw new Error(`Supplier with ID ${id} not found`);
        }

        this.mockSuppliers.splice(supplierIndex, 1);
        return undefined;
      }),
      catchError(this.handleError)
    );
  }

  private handleError(error: any): Observable<never> {
    console.error('SupplierService error:', error);

    let errorMessage = 'An error occurred while processing your request.';

    if (error.error?.message) {
      errorMessage = error.error.message;
    } else if (error.message) {
      errorMessage = error.message;
    } else if (error.status) {
      switch (error.status) {
        case 400:
          errorMessage = 'Invalid request. Please check your input.';
          break;
        case 404:
          errorMessage = 'Supplier not found.';
          break;
        case 409:
          errorMessage = 'A supplier with this information already exists.';
          break;
        case 500:
          errorMessage = 'Server error. Please try again later.';
          break;
        default:
          errorMessage = `Request failed with status ${error.status}.`;
      }
    }

    return throwError(() => new Error(errorMessage));
  }
}
