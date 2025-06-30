import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ItemSelectionStepComponent } from './item-selection-step.component';

describe('ItemSelectionStepComponent', () => {
  let component: ItemSelectionStepComponent;
  let fixture: ComponentFixture<ItemSelectionStepComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ItemSelectionStepComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ItemSelectionStepComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
