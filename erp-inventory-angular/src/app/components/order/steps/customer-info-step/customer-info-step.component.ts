import { Component, Input, OnInit, OnDestroy } from '@angular/core';
import {
  FormBuilder,
  FormGroup,
  Validators,
  ReactiveFormsModule,
} from '@angular/forms';
import { CommonModule } from '@angular/common';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { Subject, takeUntil } from 'rxjs';
import { OrderStateService } from '../../../../services/order-state.service';
import { CustomerInfo } from '../../../../models/order.model';

@Component({
  selector: 'app-customer-info-step',
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatCardModule,
    MatIconModule,
  ],
  templateUrl: './customer-info-step.component.html',
  styleUrl: './customer-info-step.component.scss',
})
export class CustomerInfoStepComponent implements OnInit, OnDestroy {
  @Input() formGroup!: FormGroup;

  private destroy$ = new Subject<void>();
  customerFormGroup!: FormGroup;

  constructor(
    private formBuilder: FormBuilder,
    private orderStateService: OrderStateService
  ) {}

  ngOnInit(): void {
    this.initializeForm();
    this.subscribeToOrderState();
    this.subscribeToFormChanges();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private initializeForm(): void {
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

    // If a form group was passed in (from parent), use it instead
    if (this.formGroup) {
      this.customerFormGroup = this.formGroup;
    }
  }

  private subscribeToOrderState(): void {
    this.orderStateService.orderState$
      .pipe(takeUntil(this.destroy$))
      .subscribe((state) => {
        if (state.customerInfo && !this.customerFormGroup.dirty) {
          // Only update form if it hasn't been modified by user
          this.customerFormGroup.patchValue(state.customerInfo, {
            emitEvent: false,
          });
        }
      });
  }

  private subscribeToFormChanges(): void {
    this.customerFormGroup.valueChanges
      .pipe(takeUntil(this.destroy$))
      .subscribe((value) => {
        if (this.customerFormGroup.valid) {
          const customerInfo: CustomerInfo = {
            name: value.name || '',
            email: value.email || '',
            phone: value.phone || '',
            notes: value.notes || '',
          };
          this.orderStateService.updateCustomerInfo(customerInfo);
        }
      });
  }

  // Getter for easy access to form controls in template
  get formControls() {
    return this.customerFormGroup.controls;
  }

  // Validation helpers
  getErrorMessage(field: string): string {
    const control = this.customerFormGroup.get(field);
    if (control?.hasError('required')) {
      return `${this.getFieldDisplayName(field)} is required`;
    }
    if (control?.hasError('email')) {
      return 'Please enter a valid email address';
    }
    if (control?.hasError('minlength')) {
      return `${this.getFieldDisplayName(field)} must be at least ${
        control.errors?.['minlength'].requiredLength
      } characters`;
    }
    if (control?.hasError('maxlength')) {
      return `${this.getFieldDisplayName(field)} cannot exceed ${
        control.errors?.['maxlength'].requiredLength
      } characters`;
    }
    return '';
  }

  private getFieldDisplayName(field: string): string {
    const displayNames: { [key: string]: string } = {
      name: 'Customer name',
      email: 'Email address',
      phone: 'Phone number',
      notes: 'Notes',
    };
    return displayNames[field] || field;
  }

  // Form validation status
  get isFormValid(): boolean {
    return this.customerFormGroup.valid;
  }

  get isFormTouched(): boolean {
    return this.customerFormGroup.touched;
  }
}
