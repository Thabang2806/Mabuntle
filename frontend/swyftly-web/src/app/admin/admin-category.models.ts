export interface AdminCategoryResponse {
  categoryId: string;
  parentCategoryId: string | null;
  name: string;
  slug: string;
  displayOrder: number;
  isActive: boolean;
  attributes: AdminCategoryAttributeResponse[];
}

export interface AdminCategoryAttributeResponse {
  attributeId: string;
  name: string;
  key: string;
  dataType: string;
  isRequired: boolean;
  allowedValues: string[];
  displayOrder: number;
  isActive: boolean;
}
