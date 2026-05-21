import { Component, OnInit, computed, inject, signal } from '@angular/core';
import {
  AbstractControl,
  NonNullableFormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  ValidatorFn,
  Validators
} from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { RouterLink } from '@angular/router';
import { getApiErrorMessage } from '../auth/api-error';
import { SellerWorkspaceNavComponent } from '../seller/seller-workspace-nav.component';
import {
  SellerOnboardingResponse,
  UpdateSellerAddressRequest,
  UpdateSellerPayoutRequest,
  UpdateSellerProfileRequest,
  UpdateSellerStorefrontRequest
} from '../seller/seller-onboarding.models';
import { SellerOnboardingService } from '../seller/seller-onboarding.service';
import { DashboardCardComponent } from '../shared/ui/dashboard-card.component';
import { StatusBadgeComponent } from '../shared/ui/status-badge.component';

type WizardStep = 0 | 1 | 2 | 3 | 4;

@Component({
  selector: 'app-seller-page',
  imports: [
    DashboardCardComponent,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    ReactiveFormsModule,
    RouterLink,
    SellerWorkspaceNavComponent,
    StatusBadgeComponent
  ],
  template: `
    <section class="page seller-onboarding">
      <app-seller-workspace-nav />

      <div class="page-header">
        <span class="eyebrow">{{ isVerified() ? 'Seller dashboard' : 'Seller onboarding' }}</span>
        <h1>Seller workspace</h1>
        <p>{{ isVerified() ? 'Manage store operations, products, payouts, support, ads, and analytics.' : 'Complete the required setup details before submitting your seller profile for review.' }}</p>
        <div class="auth-actions">
          <a mat-stroked-button routerLink="/seller/products">Products</a>
          <a mat-stroked-button routerLink="/seller/orders">Orders</a>
          <a mat-stroked-button routerLink="/seller/returns">Returns</a>
          <a mat-stroked-button routerLink="/seller/payouts">Payouts</a>
          <a mat-stroked-button routerLink="/seller/support">Support</a>
          <a mat-stroked-button routerLink="/seller/ads">Ads</a>
          <a mat-stroked-button routerLink="/seller/analytics">Analytics</a>
        </div>
      </div>

      <div class="onboarding-status">
        <span class="status-pill">{{ onboarding()?.verificationStatus ?? 'Loading' }}</span>
        @if (onboarding()) {
          <span>{{ completionLabel() }}</span>
        }
      </div>

      @if (isLoading()) {
        <div class="route-card">Loading seller onboarding...</div>
      } @else if (isVerified()) {
        <div class="seller-dashboard">
          @if (errorMessage()) {
            <p class="auth-alert error" role="alert">{{ errorMessage() }}</p>
          }

          <section class="seller-dashboard-hero">
            <div>
              <app-status-badge label="Verified seller" tone="success" />
              <h2>{{ onboarding()?.storefront?.storeName ?? onboarding()?.profile?.displayName ?? 'Seller store' }}</h2>
              <p>Use this workspace for daily store operations. Payout actions are read-only for sellers; admin finance controls stay in the admin area.</p>
            </div>
            <div class="seller-dashboard-status">
              <strong>{{ completionLabel() }}</strong>
              <span>{{ onboarding()?.storefront?.isPublished ? 'Storefront published' : 'Storefront not published' }}</span>
            </div>
          </section>

          <div class="seller-dashboard-grid">
            @for (card of dashboardCards; track card.route) {
              <app-dashboard-card [eyebrow]="card.eyebrow" [heading]="card.heading" [description]="card.description">
                <a mat-stroked-button [routerLink]="card.route">{{ card.action }}</a>
              </app-dashboard-card>
            }
          </div>
        </div>
      } @else {
        @if (errorMessage()) {
          <p class="auth-alert error" role="alert">{{ errorMessage() }}</p>
        }

        @if (successMessage()) {
          <p class="auth-alert success" role="status">{{ successMessage() }}</p>
        }

        <div class="wizard-layout">
          <nav class="wizard-steps" aria-label="Seller onboarding steps">
            @for (step of steps; track step.index) {
              <button
                type="button"
                [class.active]="currentStep() === step.index"
                [class.complete]="isStepComplete(step.index)"
                (click)="currentStep.set(step.index)"
              >
                <span>{{ step.index + 1 }}</span>
                {{ step.label }}
              </button>
            }
          </nav>

          <div class="wizard-panel">
            @switch (currentStep()) {
              @case (0) {
                <form [formGroup]="profileForm" (ngSubmit)="saveProfile()" class="wizard-form" novalidate>
                  <h2>Basic seller details</h2>
                  <mat-form-field appearance="outline">
                    <mat-label>Display name</mat-label>
                    <input matInput formControlName="displayName" />
                    @if (profileForm.controls.displayName.hasError('required')) {
                      <mat-error>Display name is required.</mat-error>
                    }
                  </mat-form-field>

                  <mat-form-field appearance="outline">
                    <mat-label>Contact email</mat-label>
                    <input matInput type="email" formControlName="contactEmail" />
                    @if (profileForm.controls.contactEmail.hasError('required')) {
                      <mat-error>Contact email is required.</mat-error>
                    } @else if (profileForm.controls.contactEmail.hasError('email')) {
                      <mat-error>Enter a valid email address.</mat-error>
                    }
                  </mat-form-field>

                  <mat-form-field appearance="outline">
                    <mat-label>Phone number</mat-label>
                    <input matInput formControlName="phoneNumber" />
                    @if (profileForm.controls.phoneNumber.hasError('required')) {
                      <mat-error>Phone number is required.</mat-error>
                    }
                  </mat-form-field>

                  <mat-form-field appearance="outline">
                    <mat-label>Business type</mat-label>
                    <mat-select formControlName="businessType">
                      <mat-option value="Individual">Individual</mat-option>
                      <mat-option value="RegisteredBusiness">Registered business</mat-option>
                    </mat-select>
                  </mat-form-field>

                  <mat-form-field appearance="outline">
                    <mat-label>Business name</mat-label>
                    <input matInput formControlName="businessName" />
                    @if (profileForm.hasError('businessNameRequired')) {
                      <mat-error>Business name is required for registered businesses.</mat-error>
                    }
                  </mat-form-field>

                  <button mat-flat-button type="submit" [disabled]="isSaving()">Save and continue</button>
                </form>
              }
              @case (1) {
                <form [formGroup]="storefrontForm" (ngSubmit)="saveStorefront()" class="wizard-form" novalidate>
                  <h2>Storefront details</h2>
                  <mat-form-field appearance="outline">
                    <mat-label>Store name</mat-label>
                    <input matInput formControlName="storeName" />
                    @if (storefrontForm.controls.storeName.hasError('required')) {
                      <mat-error>Store name is required.</mat-error>
                    }
                  </mat-form-field>

                  <mat-form-field appearance="outline">
                    <mat-label>Store slug</mat-label>
                    <input matInput formControlName="slug" />
                    @if (storefrontForm.controls.slug.hasError('required')) {
                      <mat-error>Store slug is required.</mat-error>
                    } @else if (storefrontForm.controls.slug.hasError('pattern')) {
                      <mat-error>Use lowercase letters, numbers, and hyphens.</mat-error>
                    }
                  </mat-form-field>

                  <mat-form-field appearance="outline">
                    <mat-label>Description</mat-label>
                    <textarea matInput rows="4" formControlName="description"></textarea>
                  </mat-form-field>

                  <mat-form-field appearance="outline">
                    <mat-label>Logo URL</mat-label>
                    <input matInput formControlName="logoUrl" />
                  </mat-form-field>

                  <mat-form-field appearance="outline">
                    <mat-label>Banner URL</mat-label>
                    <input matInput formControlName="bannerUrl" />
                  </mat-form-field>

                  <button mat-flat-button type="submit" [disabled]="isSaving()">Save and continue</button>
                </form>
              }
              @case (2) {
                <form [formGroup]="addressForm" (ngSubmit)="saveAddress()" class="wizard-form" novalidate>
                  <h2>Address and fulfilment</h2>
                  <mat-form-field appearance="outline">
                    <mat-label>Address line 1</mat-label>
                    <input matInput formControlName="addressLine1" />
                    @if (addressForm.controls.addressLine1.hasError('required')) {
                      <mat-error>Address line 1 is required.</mat-error>
                    }
                  </mat-form-field>

                  <mat-form-field appearance="outline">
                    <mat-label>Address line 2</mat-label>
                    <input matInput formControlName="addressLine2" />
                  </mat-form-field>

                  <div class="form-grid">
                    <mat-form-field appearance="outline">
                      <mat-label>City</mat-label>
                      <input matInput formControlName="city" />
                      @if (addressForm.controls.city.hasError('required')) {
                        <mat-error>City is required.</mat-error>
                      }
                    </mat-form-field>

                    <mat-form-field appearance="outline">
                      <mat-label>Province</mat-label>
                      <input matInput formControlName="province" />
                      @if (addressForm.controls.province.hasError('required')) {
                        <mat-error>Province is required.</mat-error>
                      }
                    </mat-form-field>
                  </div>

                  <div class="form-grid">
                    <mat-form-field appearance="outline">
                      <mat-label>Postal code</mat-label>
                      <input matInput formControlName="postalCode" />
                      @if (addressForm.controls.postalCode.hasError('required')) {
                        <mat-error>Postal code is required.</mat-error>
                      }
                    </mat-form-field>

                    <mat-form-field appearance="outline">
                      <mat-label>Country code</mat-label>
                      <input matInput maxlength="2" formControlName="countryCode" />
                      @if (addressForm.controls.countryCode.hasError('required')) {
                        <mat-error>Country code is required.</mat-error>
                      } @else if (addressForm.controls.countryCode.hasError('pattern')) {
                        <mat-error>Use a 2-letter country code.</mat-error>
                      }
                    </mat-form-field>
                  </div>

                  <button mat-flat-button type="submit" [disabled]="isSaving()">Save and continue</button>
                </form>
              }
              @case (3) {
                <form [formGroup]="payoutForm" (ngSubmit)="savePayout()" class="wizard-form" novalidate>
                  <h2>Payout setup</h2>
                  <p>No bank details are stored here. Use a provider reference or setup token for now.</p>
                  <mat-form-field appearance="outline">
                    <mat-label>Payout provider reference</mat-label>
                    <input matInput formControlName="payoutProviderReference" />
                    @if (payoutForm.controls.payoutProviderReference.hasError('required')) {
                      <mat-error>Payout provider reference is required.</mat-error>
                    }
                  </mat-form-field>

                  <button mat-flat-button type="submit" [disabled]="isSaving()">Save and continue</button>
                </form>
              }
              @case (4) {
                <div class="review-panel">
                  <h2>Review and submit</h2>
                  <div class="review-grid">
                    @for (item of reviewItems(); track item.label) {
                      <article class="route-card">
                        <span class="status-pill">{{ item.complete ? 'Complete' : 'Missing' }}</span>
                        <h2>{{ item.label }}</h2>
                        <p>{{ item.summary }}</p>
                      </article>
                    }
                  </div>

                  <button
                    mat-flat-button
                    type="button"
                    [disabled]="!onboarding()?.canSubmitForVerification || isSaving()"
                    (click)="submitVerification()"
                  >
                    Submit for verification
                  </button>

                  @if (!onboarding()?.canSubmitForVerification) {
                    <p>Complete every onboarding step before submitting for review.</p>
                  }
                </div>
              }
            }
          </div>
        </div>
      }
    </section>
  `
})
export class SellerPageComponent implements OnInit {
  private readonly formBuilder = inject(NonNullableFormBuilder);
  private readonly onboardingService = inject(SellerOnboardingService);

  protected readonly onboarding = signal<SellerOnboardingResponse | null>(null);
  protected readonly currentStep = signal<WizardStep>(0);
  protected readonly isLoading = signal(true);
  protected readonly isSaving = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly successMessage = signal<string | null>(null);

  protected readonly steps: readonly { index: WizardStep; label: string }[] = [
    { index: 0, label: 'Details' },
    { index: 1, label: 'Storefront' },
    { index: 2, label: 'Address' },
    { index: 3, label: 'Payout' },
    { index: 4, label: 'Review' }
  ];

  protected readonly dashboardCards: readonly {
    eyebrow: string;
    heading: string;
    description: string;
    route: string;
    action: string;
  }[] = [
    {
      eyebrow: 'Catalog',
      heading: 'Products',
      description: 'Manage drafts, review submissions, images, variants, stock, and listing quality.',
      route: '/seller/products',
      action: 'Manage products'
    },
    {
      eyebrow: 'Fulfilment',
      heading: 'Orders',
      description: 'Review seller orders, add tracking, and move orders through manual fulfilment.',
      route: '/seller/orders',
      action: 'View orders'
    },
    {
      eyebrow: 'After-sales',
      heading: 'Returns',
      description: 'Respond to return requests and keep buyer-facing return decisions clear.',
      route: '/seller/returns',
      action: 'Review returns'
    },
    {
      eyebrow: 'Finance',
      heading: 'Payouts',
      description: 'Read seller balances and payout history without exposing admin finance controls.',
      route: '/seller/payouts',
      action: 'View payouts'
    },
    {
      eyebrow: 'Support',
      heading: 'Support',
      description: 'Create and follow seller support tickets linked to orders, products, or operations.',
      route: '/seller/support',
      action: 'Open support'
    },
    {
      eyebrow: 'Growth',
      heading: 'Ads and analytics',
      description: 'Access existing advertising and aggregate analytics tools after core operations.',
      route: '/seller/analytics',
      action: 'View analytics'
    }
  ];

  protected readonly isVerified = computed(() => this.onboarding()?.verificationStatus === 'Verified');

  protected readonly completionLabel = computed(() => {
    const onboarding = this.onboarding();
    if (!onboarding) {
      return '';
    }

    const completed = [
      onboarding.isProfileComplete,
      onboarding.isStorefrontComplete,
      onboarding.isAddressComplete,
      onboarding.isPayoutPlaceholderComplete
    ].filter(Boolean).length;

    return `${completed} of 4 setup sections complete`;
  });

  protected readonly profileForm = this.formBuilder.group({
    displayName: ['', [Validators.required]],
    contactEmail: ['', [Validators.required, Validators.email]],
    phoneNumber: ['', [Validators.required]],
    businessType: ['Individual' as 'Individual' | 'RegisteredBusiness', [Validators.required]],
    businessName: ['']
  }, { validators: [businessNameRequiredValidator()] });

  protected readonly storefrontForm = this.formBuilder.group({
    storeName: ['', [Validators.required]],
    slug: ['', [Validators.required, Validators.pattern(/^[a-z0-9-]+$/)]],
    description: [''],
    logoUrl: [''],
    bannerUrl: ['']
  });

  protected readonly addressForm = this.formBuilder.group({
    addressLine1: ['', [Validators.required]],
    addressLine2: [''],
    city: ['', [Validators.required]],
    province: ['', [Validators.required]],
    postalCode: ['', [Validators.required]],
    countryCode: ['ZA', [Validators.required, Validators.pattern(/^[A-Za-z]{2}$/)]]
  });

  protected readonly payoutForm = this.formBuilder.group({
    payoutProviderReference: ['', [Validators.required]]
  });

  async ngOnInit(): Promise<void> {
    await this.loadOnboarding();
  }

  protected async saveProfile(): Promise<void> {
    if (!this.ensureValid(this.profileForm)) {
      return;
    }

    const value = this.profileForm.getRawValue();
    await this.saveStep(
      () => this.onboardingService.updateProfile({
        displayName: value.displayName,
        contactEmail: value.contactEmail,
        phoneNumber: value.phoneNumber,
        businessType: value.businessType,
        businessName: emptyToNull(value.businessName)
      } satisfies UpdateSellerProfileRequest),
      1);
  }

  protected async saveStorefront(): Promise<void> {
    if (!this.ensureValid(this.storefrontForm)) {
      return;
    }

    const value = this.storefrontForm.getRawValue();
    await this.saveStep(
      () => this.onboardingService.updateStorefront({
        storeName: value.storeName,
        slug: value.slug,
        description: emptyToNull(value.description),
        logoUrl: emptyToNull(value.logoUrl),
        bannerUrl: emptyToNull(value.bannerUrl)
      } satisfies UpdateSellerStorefrontRequest),
      2);
  }

  protected async saveAddress(): Promise<void> {
    if (!this.ensureValid(this.addressForm)) {
      return;
    }

    const value = this.addressForm.getRawValue();
    await this.saveStep(
      () => this.onboardingService.updateAddress({
        addressLine1: value.addressLine1,
        addressLine2: emptyToNull(value.addressLine2),
        city: value.city,
        province: value.province,
        postalCode: value.postalCode,
        countryCode: value.countryCode.toUpperCase()
      } satisfies UpdateSellerAddressRequest),
      3);
  }

  protected async savePayout(): Promise<void> {
    if (!this.ensureValid(this.payoutForm)) {
      return;
    }

    const value = this.payoutForm.getRawValue();
    await this.saveStep(
      () => this.onboardingService.updatePayout({
        payoutProviderReference: value.payoutProviderReference
      } satisfies UpdateSellerPayoutRequest),
      4);
  }

  protected async submitVerification(): Promise<void> {
    await this.saveStep(
      () => this.onboardingService.submitVerification(),
      4,
      'Seller profile submitted for verification.');
  }

  protected isStepComplete(step: WizardStep): boolean {
    const onboarding = this.onboarding();
    if (!onboarding) {
      return false;
    }

    return [
      onboarding.isProfileComplete,
      onboarding.isStorefrontComplete,
      onboarding.isAddressComplete,
      onboarding.isPayoutPlaceholderComplete,
      onboarding.verificationStatus === 'UnderReview' || onboarding.verificationStatus === 'Verified'
    ][step];
  }

  protected reviewItems(): readonly { label: string; complete: boolean; summary: string }[] {
    const onboarding = this.onboarding();

    return [
      {
        label: 'Profile',
        complete: onboarding?.isProfileComplete ?? false,
        summary: onboarding?.profile.displayName ?? 'Seller profile details are required.'
      },
      {
        label: 'Storefront',
        complete: onboarding?.isStorefrontComplete ?? false,
        summary: onboarding?.storefront?.storeName ?? 'Storefront name and slug are required.'
      },
      {
        label: 'Address',
        complete: onboarding?.isAddressComplete ?? false,
        summary: onboarding?.address?.city ?? 'Fulfilment address is required.'
      },
      {
        label: 'Payout',
        complete: onboarding?.isPayoutPlaceholderComplete ?? false,
        summary: onboarding?.payout?.payoutProviderReference ?? 'A provider reference is required.'
      }
    ];
  }

  private async loadOnboarding(): Promise<void> {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    try {
      const onboarding = await this.onboardingService.getOnboarding();
      this.setOnboarding(onboarding);
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isLoading.set(false);
    }
  }

  private async saveStep(
    action: () => Promise<SellerOnboardingResponse>,
    nextStep: WizardStep,
    successMessage = 'Saved.')
  {
    this.errorMessage.set(null);
    this.successMessage.set(null);
    this.isSaving.set(true);

    try {
      const onboarding = await action();
      this.setOnboarding(onboarding);
      this.currentStep.set(nextStep);
      this.successMessage.set(successMessage);
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
    } finally {
      this.isSaving.set(false);
    }
  }

  private setOnboarding(onboarding: SellerOnboardingResponse): void {
    this.onboarding.set(onboarding);

    this.profileForm.patchValue({
      displayName: onboarding.profile.displayName ?? '',
      contactEmail: onboarding.profile.contactEmail ?? '',
      phoneNumber: onboarding.profile.phoneNumber ?? '',
      businessType: onboarding.profile.businessType === 'RegisteredBusiness'
        ? 'RegisteredBusiness'
        : 'Individual',
      businessName: onboarding.profile.businessName ?? ''
    });

    this.storefrontForm.patchValue({
      storeName: onboarding.storefront?.storeName ?? '',
      slug: onboarding.storefront?.slug ?? '',
      description: onboarding.storefront?.description ?? '',
      logoUrl: onboarding.storefront?.logoUrl ?? '',
      bannerUrl: onboarding.storefront?.bannerUrl ?? ''
    });

    this.addressForm.patchValue({
      addressLine1: onboarding.address?.addressLine1 ?? '',
      addressLine2: onboarding.address?.addressLine2 ?? '',
      city: onboarding.address?.city ?? '',
      province: onboarding.address?.province ?? '',
      postalCode: onboarding.address?.postalCode ?? '',
      countryCode: onboarding.address?.countryCode ?? 'ZA'
    });

    this.payoutForm.patchValue({
      payoutProviderReference: onboarding.payout?.payoutProviderReference ?? ''
    });
  }

  private ensureValid(control: AbstractControl): boolean {
    if (control.invalid || this.isSaving()) {
      control.markAllAsTouched();
      return false;
    }

    return true;
  }
}

function businessNameRequiredValidator(): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const businessType = control.get('businessType')?.value;
    const businessName = control.get('businessName')?.value;

    return businessType === 'RegisteredBusiness' && !businessName?.trim()
      ? { businessNameRequired: true }
      : null;
  };
}

function emptyToNull(value: string): string | null {
  const trimmed = value.trim();
  return trimmed.length === 0 ? null : trimmed;
}
