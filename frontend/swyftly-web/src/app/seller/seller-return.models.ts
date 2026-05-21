export interface SellerReturnRequestResult {
  returnRequestId: string;
  orderId: string;
  buyerId: string;
  sellerId: string;
  status: string;
  reason: string;
  details: string | null;
  requestedAtUtc: string;
  sellerRespondedAtUtc: string | null;
  sellerResponseReason: string | null;
  disputedAtUtc: string | null;
  disputeReason: string | null;
  items: SellerReturnItemResult[];
  messages: SellerReturnMessageResult[];
}

export interface SellerReturnItemResult {
  returnItemId: string;
  orderItemId: string;
  productId: string;
  productVariantId: string;
  quantity: number;
  reason: string;
  isOpenedOrUnsealed: boolean;
  note: string | null;
}

export interface SellerReturnMessageResult {
  returnMessageId: string;
  senderUserId: string;
  senderRole: string;
  message: string;
  createdAtUtc: string;
}

export interface SellerReturnResponseRequest {
  message: string | null;
}
