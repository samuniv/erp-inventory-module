import { ComponentFixture, TestBed } from '@angular/core/testing';

import { InventoryFormDialogComponent } from './inventory-form-dialog.component';

describe('InventoryFormDialogComponent', () => {
  let component: InventoryFormDialogComponent;
  let fixture: ComponentFixture<InventoryFormDialogComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [InventoryFormDialogComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(InventoryFormDialogComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
