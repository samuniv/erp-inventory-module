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
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatStepperModule } from '@angular/material/stepper';

import {
  InventoryItem,
  CreateInventoryItemDto,
  UpdateInventoryItemDto,
} from '../../../models/inventory.model';

export interface InventoryFormDialogData {
  mode: 'create' | 'edit';
  item?: InventoryItem;
  categories: string[];
  suppliers: { id: number; name: string }[];
}

@Component({
  selector: 'app-inventory-form-dialog',
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatCheckboxModule,
    MatStepperModule,
  ],
  templateUrl: './inventory-form-dialog.component.html',
  styleUrl: './inventory-form-dialog.component.scss',
})
export class InventoryFormDialogComponent implements OnInit {
  inventoryForm: FormGroup;
  isEditMode: boolean;

  constructor(
    private fb: FormBuilder,
    private dialogRef: MatDialogRef<InventoryFormDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: InventoryFormDialogData
  ) {
    this.isEditMode = data.mode === 'edit';
    this.inventoryForm = this.createForm();
  }

  ngOnInit(): void {
    if (this.isEditMode && this.data.item) {
      this.populateForm(this.data.item);
    }
  }

  private createForm(): FormGroup {
    return this.fb.group({
      name: ['', [Validators.required, Validators.maxLength(200)]],
      description: ['', [Validators.maxLength(500)]],
      sku: ['', [Validators.required, Validators.maxLength(50)]],
      price: [0, [Validators.required, Validators.min(0)]],
      stockLevel: [0, [Validators.required, Validators.min(0)]],
      reorderThreshold: [0, [Validators.required, Validators.min(0)]],
      category: ['', [Validators.required]],
      supplierId: [null],
      isActive: [true],
    });
  }

  private populateForm(item: InventoryItem): void {
    this.inventoryForm.patchValue({
      name: item.name,
      description: item.description,
      sku: item.sku,
      price: item.price,
      stockLevel: item.stockLevel,
      reorderThreshold: item.reorderThreshold,
      category: item.category,
      supplierId: item.supplierId,
      isActive: item.isActive,
    });
  }

  onSubmit(): void {
    if (this.inventoryForm.valid) {
      const formValue = this.inventoryForm.value;

      if (this.isEditMode) {
        const updateDto: UpdateInventoryItemDto = {
          name: formValue.name,
          description: formValue.description,
          sku: formValue.sku,
          price: formValue.price,
          stockLevel: formValue.stockLevel,
          reorderThreshold: formValue.reorderThreshold,
          category: formValue.category,
          supplierId: formValue.supplierId,
          isActive: formValue.isActive,
        };
        this.dialogRef.close({ action: 'update', data: updateDto });
      } else {
        const createDto: CreateInventoryItemDto = {
          name: formValue.name,
          description: formValue.description,
          sku: formValue.sku,
          price: formValue.price,
          stockLevel: formValue.stockLevel,
          reorderThreshold: formValue.reorderThreshold,
          category: formValue.category,
          supplierId: formValue.supplierId,
        };
        this.dialogRef.close({ action: 'create', data: createDto });
      }
    } else {
      this.markFormGroupTouched();
    }
  }

  onCancel(): void {
    this.dialogRef.close();
  }

  getErrorMessage(controlName: string): string {
    const control = this.inventoryForm.get(controlName);
    if (control?.hasError('required')) {
      return `${this.getFieldName(controlName)} is required`;
    }
    if (control?.hasError('maxlength')) {
      const maxLength = control.errors?.['maxlength']?.requiredLength;
      return `${this.getFieldName(
        controlName
      )} cannot exceed ${maxLength} characters`;
    }
    if (control?.hasError('min')) {
      return `${this.getFieldName(controlName)} must be a positive number`;
    }
    return '';
  }

  private getFieldName(controlName: string): string {
    const fieldNames: { [key: string]: string } = {
      name: 'Name',
      description: 'Description',
      sku: 'SKU',
      price: 'Price',
      stockLevel: 'Stock Level',
      reorderThreshold: 'Reorder Threshold',
      category: 'Category',
    };
    return fieldNames[controlName] || controlName;
  }

  private markFormGroupTouched(): void {
    Object.keys(this.inventoryForm.controls).forEach((key) => {
      const control = this.inventoryForm.get(key);
      control?.markAsTouched();
    });
  }

  get title(): string {
    return this.isEditMode ? 'Edit Inventory Item' : 'Add New Inventory Item';
  }

  get submitButtonText(): string {
    return this.isEditMode ? 'Update Item' : 'Create Item';
  }
}
