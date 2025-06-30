import { ComponentFixture, TestBed } from '@angular/core/testing';

import { OrderReviewStepComponent } from './order-review-step.component';

describe('OrderReviewStepComponent', () => {
  let component: OrderReviewStepComponent;
  let fixture: ComponentFixture<OrderReviewStepComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [OrderReviewStepComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(OrderReviewStepComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
