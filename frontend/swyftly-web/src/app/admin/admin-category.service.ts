import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import { AdminCategoryResponse } from './admin-category.models';

@Injectable({ providedIn: 'root' })
export class AdminCategoryService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/admin/categories`;

  listCategories(): Promise<AdminCategoryResponse[]> {
    return firstValueFrom(this.http.get<AdminCategoryResponse[]>(this.baseUrl));
  }
}
