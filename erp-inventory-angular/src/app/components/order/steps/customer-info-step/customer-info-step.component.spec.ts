import { ComponentFixture, TestBed } from '@angular/core/testing';

import { CustomerInfoStepComponent } from './customer-info-step.component';

describe('CustomerInfoStepComponent', () => {
  let component: CustomerInfoStepComponent;
  let fixture: ComponentFixture<CustomerInfoStepComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CustomerInfoStepComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(CustomerInfoStepComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
