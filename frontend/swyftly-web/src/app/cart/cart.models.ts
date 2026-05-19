export interface CartResponse {
  cartId: string | null;
  buyerId: string | null;
  sellerId: string | null;
  sellerStoreName: string | null;
  items: CartItemResponse[];
  totalQuantity: number;
  subtotal: number;
}

export interface CartItemResponse {
  cartItemId: string;
  productId: string;
  productVariantId: string;
  productTitle: string | null;
  sku: string;
  size: string;
  colour: string;
  unitPrice: number;
  quantity: number;
  lineTotal: number;
}

export interface AddCartItemRequest {
  productVariantId: string;
  quantity: number;
}

export interface UpdateCartItemRequest {
  quantity: number;
}

export interface CreateOrderFromCartRequest {
  cartId: string | null;
  reservationMinutes: number | null;
}

export interface OrderResult {
  orderId: string;
  buyerId: string;
  sellerId: string;
  cartId: string;
  status: string;
  items: OrderItemResult[];
  itemsSubtotal: number;
  shippingAmount: number;
  platformFeeAmount: number;
  discountAmount: number;
  totalAmount: number;
  statusHistory: OrderStatusHistoryResult[];
}

export interface OrderItemResult {
  orderItemId: string;
  productId: string;
  productVariantId: string;
  productTitle: string | null;
  sku: string;
  size: string;
  colour: string;
  unitPrice: number;
  quantity: number;
  lineTotal: number;
}

export interface OrderStatusHistoryResult {
  statusHistoryId: string;
  previousStatus: string | null;
  newStatus: string;
  changedAtUtc: string;
  reason: string | null;
}
