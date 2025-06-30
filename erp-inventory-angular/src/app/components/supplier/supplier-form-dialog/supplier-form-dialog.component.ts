import { Component, Inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  ReactiveFormsModule,
  FormBuilder,
  FormGroup,
  Validators,
} from '@angular/forms';
import {
  MatDialogModule,
  MatDialogRef,
  MAT_DIALOG_DATA,
} from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

import {
  Supplier,
  CreateSupplierDto,
  UpdateSupplierDto,
} from '../../../models/supplier.model';

export interface SupplierFormDialogData {
  mode: 'create' | 'edit';
  supplier?: Supplier;
}

export interface SupplierFormDialogResult {
  action: 'create' | 'update' | 'cancel';
  data?: CreateSupplierDto | UpdateSupplierDto;
}

@Component({
  selector: 'app-supplier-form-dialog',
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatCheckboxModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './supplier-form-dialog.component.html',
  styleUrl: './supplier-form-dialog.component.scss',
})
export class SupplierFormDialogComponent implements OnInit {
  supplierForm!: FormGroup;
  isSubmitting = false;

  constructor(
    private fb: FormBuilder,
    private dialogRef: MatDialogRef<SupplierFormDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: SupplierFormDialogData
  ) {}

  ngOnInit(): void {
    this.initializeForm();

    if (this.data.mode === 'edit' && this.data.supplier) {
      this.populateForm(this.data.supplier);
    }
  }

  private initializeForm(): void {
    this.supplierForm = this.fb.group({
      name: [
        '',
        [
          Validators.required,
          Validators.minLength(2),
          Validators.maxLength(100),
        ],
      ],
      contactPerson: [
        '',
        [
          Validators.required,
          Validators.minLength(2),
          Validators.maxLength(50),
        ],
      ],
      email: [
        '',
        [Validators.required, Validators.email, Validators.maxLength(100)],
      ],
      phone: [
        '',
        [
          Validators.required,
          Validators.pattern(/^[\+]?[(]?[\d\s\-\(\)]{10,}$/),
        ],
      ],
      address: [
        '',
        [
          Validators.required,
          Validators.minLength(10),
          Validators.maxLength(200),
        ],
      ],
      isActive: [true],
    });
  }

  private populateForm(supplier: Supplier): void {
    this.supplierForm.patchValue({
      name: supplier.name,
      contactPerson: supplier.contactPerson,
      email: supplier.email,
      phone: supplier.phone,
      address: supplier.address,
      isActive: supplier.isActive,
    });
  }

  onSubmit(): void {
    if (this.supplierForm.valid && !this.isSubmitting) {
      this.isSubmitting = true;

      const formValue = this.supplierForm.value;
      const result: SupplierFormDialogResult = {
        action: this.data.mode === 'create' ? 'create' : 'update',
        data: formValue,
      };

      // Simulate processing delay
      setTimeout(() => {
        this.dialogRef.close(result);
      }, 500);
    } else {
      this.markFormGroupTouched();
    }
  }

  onCancel(): void {
    const result: SupplierFormDialogResult = {
      action: 'cancel',
    };
    this.dialogRef.close(result);
  }

  private markFormGroupTouched(): void {
    Object.keys(this.supplierForm.controls).forEach((key) => {
      const control = this.supplierForm.get(key);
      if (control) {
        control.markAsTouched();
      }
    });
  }

  getFieldErrorMessage(fieldName: string): string {
    const control = this.supplierForm.get(fieldName);
    if (control && control.errors && control.touched) {
      const errors = control.errors;

      if (errors['required']) {
        return `${this.getFieldDisplayName(fieldName)} is required.`;
      }

      if (errors['minlength']) {
        const requiredLength = errors['minlength'].requiredLength;
        return `${this.getFieldDisplayName(
          fieldName
        )} must be at least ${requiredLength} characters long.`;
      }

      if (errors['maxlength']) {
        const requiredLength = errors['maxlength'].requiredLength;
        return `${this.getFieldDisplayName(
          fieldName
        )} cannot exceed ${requiredLength} characters.`;
      }

      if (errors['email']) {
        return 'Please enter a valid email address.';
      }

      if (errors['pattern']) {
        if (fieldName === 'phone') {
          return 'Please enter a valid phone number.';
        }
      }
    }

    return '';
  }

  private getFieldDisplayName(fieldName: string): string {
    const displayNames: { [key: string]: string } = {
      name: 'Supplier name',
      contactPerson: 'Contact person',
      email: 'Email address',
      phone: 'Phone number',
      address: 'Address',
    };

    return displayNames[fieldName] || fieldName;
  }

  isFieldInvalid(fieldName: string): boolean {
    const control = this.supplierForm.get(fieldName);
    return !!(control && control.invalid && control.touched);
  }

  get dialogTitle(): string {
    return this.data.mode === 'create' ? 'Add New Supplier' : 'Edit Supplier';
  }

  get submitButtonText(): string {
    if (this.isSubmitting) {
      return this.data.mode === 'create' ? 'Creating...' : 'Updating...';
    }
    return this.data.mode === 'create' ? 'Create Supplier' : 'Update Supplier';
  }
}
