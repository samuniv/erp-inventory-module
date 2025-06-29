import { Component } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { ReactiveFormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { AuthService } from '../../../services/auth/auth.service';

@Component({
  selector: 'app-login',
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatCheckboxModule,
    MatSnackBarModule,
  ],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss',
})
export class LoginComponent {
  loginForm: FormGroup;
  hidePassword = true;
  isLoading = false;
  errorMessage = '';

  constructor(
    private fb: FormBuilder,
    private router: Router,
    private route: ActivatedRoute,
    private authService: AuthService,
    private snackBar: MatSnackBar
  ) {
    this.loginForm = this.fb.group({
      email: ['admin@erp.com', [Validators.required, Validators.email]],
      password: ['password', [Validators.required]],
      rememberMe: [false],
    });

    // Check if user is already authenticated
    if (this.authService.isAuthenticated()) {
      this.navigateToRedirectUrl();
    }
  }

  onSubmit(): void {
    if (this.loginForm.valid && !this.isLoading) {
      this.isLoading = true;
      this.errorMessage = '';

      const credentials = {
        email: this.loginForm.value.email,
        password: this.loginForm.value.password,
      };

      this.authService.login(credentials).subscribe({
        next: (success) => {
          this.isLoading = false;
          if (success) {
            this.snackBar.open('Login successful!', 'Close', {
              duration: 3000,
              panelClass: ['success-snackbar'],
            });
            this.navigateToRedirectUrl();
          } else {
            this.errorMessage = 'Invalid email or password. Please try again.';
            this.snackBar.open(
              'Login failed. Please check your credentials.',
              'Close',
              {
                duration: 5000,
                panelClass: ['error-snackbar'],
              }
            );
          }
        },
        error: (error) => {
          this.isLoading = false;
          this.errorMessage =
            'An error occurred during login. Please try again.';
          this.snackBar.open('Login error. Please try again later.', 'Close', {
            duration: 5000,
            panelClass: ['error-snackbar'],
          });
          console.error('Login error:', error);
        },
      });
    } else {
      // Mark all fields as touched to show validation errors
      this.markFormGroupTouched(this.loginForm);
    }
  }

  onForgotPassword(event: Event): void {
    event.preventDefault();
    this.snackBar.open('Password reset functionality coming soon!', 'Close', {
      duration: 3000,
      panelClass: ['info-snackbar'],
    });
  }

  private navigateToRedirectUrl(): void {
    const redirectUrl =
      this.route.snapshot.queryParams['redirectUrl'] || '/dashboard';
    this.router.navigate([redirectUrl]);
  }

  private markFormGroupTouched(formGroup: FormGroup): void {
    Object.keys(formGroup.controls).forEach((key) => {
      const control = formGroup.get(key);
      control?.markAsTouched();

      if (control instanceof FormGroup) {
        this.markFormGroupTouched(control);
      }
    });
  }

  // Helper methods for template
  getFieldError(fieldName: string): string {
    const field = this.loginForm.get(fieldName);
    if (field?.hasError('required')) {
      return `${
        fieldName.charAt(0).toUpperCase() + fieldName.slice(1)
      } is required`;
    }
    if (field?.hasError('email')) {
      return 'Please enter a valid email address';
    }
    return '';
  }

  isFieldInvalid(fieldName: string): boolean {
    const field = this.loginForm.get(fieldName);
    return !!(field?.invalid && (field?.dirty || field?.touched));
  }
}
