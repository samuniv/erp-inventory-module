import { ComponentFixture, TestBed } from '@angular/core/testing';

import { SupplierFormDialogComponent } from './supplier-form-dialog.component';

describe('SupplierFormDialogComponent', () => {
  let component: SupplierFormDialogComponent;
  let fixture: ComponentFixture<SupplierFormDialogComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SupplierFormDialogComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(SupplierFormDialogComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
