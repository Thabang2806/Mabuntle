import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { AdminAuditLogDetailResponse } from '../admin/admin-audit-log.models';
import { AdminAuditLogService } from '../admin/admin-audit-log.service';
import { getApiErrorMessage } from '../auth/api-error';

@Component({
  selector: 'app-admin-audit-logs-page',
  imports: [
    DatePipe,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    ReactiveFormsModule,
    RouterLink
  ],
  template: `
    <section class="page admin-review">
      <a class="admin-back-link" routerLink="/admin">Back to admin</a>

      <div class="page-header">
        <span class="eyebrow">Admin audit</span>
        <h1>Audit logs</h1>
        <p>Review sensitive admin actions across sellers, products, and future finance workflows.</p>
      </div>

      <form [formGroup]="filtersForm" (ngSubmit)="search()" class="route-card admin-audit-filters" novalidate>
        <mat-form-field appearance="outline">
          <mat-label>Action type</mat-label>
          <input matInput formControlName="actionType">
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Entity type</mat-label>
          <input matInput formControlName="entityType">
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Entity id</mat-label>
          <input matInput formControlName="entityId">
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Actor user id</mat-label>
          <input matInput formControlName="actorUserId">
        </mat-form-field>

        <div class="admin-audit-actions">
          <button mat-flat-button type="submit" [disabled]="isLoading()">Apply filters</button>
          <button mat-stroked-button type="button" [disabled]="isLoading()" (click)="clearFilters()">Clear</button>
        </div>
      </form>

      @if (isLoading()) {
        <div class="route-card">Loading audit logs...</div>
      } @else {
        @if (errorMessage()) {
          <p class="auth-alert error" role="alert">{{ errorMessage() }}</p>
        }

        @if (auditLogs().length === 0 && !errorMessage()) {
          <div class="route-card">
            <span class="status-pill">Empty</span>
            <h2>No audit logs found</h2>
            <p>Sensitive admin actions will appear here after they are recorded.</p>
          </div>
        } @else {
          <div class="admin-table audit-table" role="table" aria-label="Admin audit logs">
            <div class="admin-table-row heading" role="row">
              <span role="columnheader">Action</span>
              <span role="columnheader">Entity</span>
              <span role="columnheader">Actor</span>
              <span role="columnheader">Created</span>
              <span role="columnheader">Reason</span>
            </div>

            @for (log of auditLogs(); track log.id) {
              <div class="admin-table-row" role="row">
                <span role="cell">
                  <strong>{{ log.actionType }}</strong>
                  <small>{{ log.ipAddress ?? 'No IP recorded' }}</small>
                </span>
                <span role="cell">
                  <strong>{{ log.entityType }}</strong>
                  <small>{{ log.entityId ?? 'No entity id' }}</small>
                </span>
                <span role="cell">
                  <strong>{{ log.actorRole ?? 'Unknown role' }}</strong>
                  <small>{{ log.actorUserId ?? 'No actor id' }}</small>
                </span>
                <span role="cell">{{ log.createdAtUtc | date:'medium' }}</span>
                <span role="cell">{{ log.reason ?? 'No reason' }}</span>
              </div>
            }
          </div>

          <p class="audit-count">{{ totalCount() }} audit log{{ totalCount() === 1 ? '' : 's' }}</p>
        }
      }
    </section>
  `
})
export class AdminAuditLogsPageComponent implements OnInit {
  private readonly formBuilder = inject(NonNullableFormBuilder);
  private readonly auditLogService = inject(AdminAuditLogService);

  protected readonly auditLogs = signal<AdminAuditLogDetailResponse[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly isLoading = signal(true);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly filtersForm = this.formBuilder.group({
    actionType: [''],
    entityType: [''],
    entityId: [''],
    actorUserId: ['']
  });

  async ngOnInit(): Promise<void> {
    await this.search();
  }

  protected async search(): Promise<void> {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    try {
      const filters = this.filtersForm.getRawValue();
      const response = await this.auditLogService.search({
        ...filters,
        pageSize: 50
      });
      this.auditLogs.set(response.items);
      this.totalCount.set(response.totalCount);
    } catch (error) {
      this.errorMessage.set(getApiErrorMessage(error));
      this.auditLogs.set([]);
      this.totalCount.set(0);
    } finally {
      this.isLoading.set(false);
    }
  }

  protected async clearFilters(): Promise<void> {
    this.filtersForm.reset();
    await this.search();
  }
}
