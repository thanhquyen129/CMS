"use client";

import { useState } from "react";

export type OrderLinkItem = {
  id: string;
  orderNo: string;
  customerName?: string;
  hasFinancialDependencies?: boolean;
};

export type ShipmentLinkItem = {
  id: string;
  shipmentNo: string;
  hasRatingDependency?: boolean;
};

export type AvailableOption = {
  id: string;
  orderNo?: string;
  shipmentNo?: string;
  customerName?: string;
};

export type BillRelationsProps = {
  billId: string;
  billNo?: string;
  billCustomerId?: string;
  billCustomerName: string;
  orders?: OrderLinkItem[];
  shipments?: ShipmentLinkItem[];
  availableOrders?: AvailableOption[];
  availableShipments?: AvailableOption[];
  onAddOrder?: (orderId: string) => void;
  onRemoveOrder?: (orderId: string) => void;
  onAddShipment?: (shipmentId: string) => void;
  onRemoveShipment?: (shipmentId: string) => void;
};

export function BillRelationsPanel({
  billCustomerName,
  orders = [],
  shipments = [],
  availableOrders = [],
  availableShipments = [],
  onAddOrder,
  onRemoveOrder,
  onAddShipment,
  onRemoveShipment,
}: BillRelationsProps) {
  const [warning, setWarning] = useState<string | null>(null);
  const [selectedOrderToAdd, setSelectedOrderToAdd] = useState("");
  const [selectedShipmentToAdd, setSelectedShipmentToAdd] = useState("");

  const hasCustomerConflict = orders.some(
    (o) => o.customerName && o.customerName !== billCustomerName
  );

  return (
    <div className="card relations-panel space-y-4" data-testid="bill-relations-panel">
      <div className="card-header border-b pb-3">
        <h3 className="text-lg font-bold text-gray-900">
          Liên kết nghiệp vụ N:N (Đơn hàng &amp; Lô hàng)
        </h3>
        <p className="text-xs text-gray-500">
          Quan hệ nhiều - nhiều độc lập. Cho phép liên kết nhiều đơn hàng/lô hàng với một Bill mà không tự động ghi đè khách hàng của Bill.
        </p>
      </div>

      {hasCustomerConflict && (
        <div
          className="alert alert-warning bg-amber-50 border-l-4 border-amber-500 p-3 rounded text-amber-900 text-sm"
          role="alert"
          data-testid="customer-conflict-alert"
        >
          <strong>Cảnh báo xung đột khách hàng:</strong> Có đơn hàng mang thông tin khách hàng khác với khách hàng chính của Bill ({billCustomerName}). Hệ thống bảo lưu quan hệ N:N và không ghi đè dữ liệu gốc.
        </div>
      )}

      {warning && (
        <div
          className="alert alert-error bg-red-50 border-l-4 border-red-500 p-3 rounded text-red-900 text-sm"
          role="alert"
          data-testid="dependency-warning"
        >
          {warning}
        </div>
      )}

      <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
        {/* Orders Box */}
        <div className="border rounded-lg p-4 bg-slate-50/50">
          <div className="flex justify-between items-center mb-3">
            <h4 className="font-semibold text-gray-800 text-sm">
              Đơn hàng liên quan ({orders.length})
            </h4>
          </div>

          {availableOrders.length > 0 && (
            <div className="flex gap-2 mb-3">
              <label htmlFor="order-select" className="sr-only">Thêm đơn hàng</label>
              <select
                id="order-select"
                aria-label="Thêm đơn hàng"
                className="text-xs border rounded p-1.5 flex-1 bg-white"
                value={selectedOrderToAdd}
                onChange={(e) => setSelectedOrderToAdd(e.target.value)}
              >
                <option value="">-- Chọn đơn hàng để liên kết --</option>
                {availableOrders.map((o) => (
                  <option key={o.id} value={o.id}>
                    {o.orderNo} ({o.customerName || "Chưa gán KH"})
                  </option>
                ))}
              </select>
              <button
                type="button"
                className="btn btn-sm text-xs px-2.5 py-1 bg-blue-600 text-white rounded hover:bg-blue-700"
                disabled={!selectedOrderToAdd}
                onClick={() => {
                  if (selectedOrderToAdd && onAddOrder) {
                    onAddOrder(selectedOrderToAdd);
                    setSelectedOrderToAdd("");
                  }
                }}
              >
                + Liên kết đơn
              </button>
            </div>
          )}

          {orders.length === 0 ? (
            <p className="text-xs text-gray-400 italic py-2">Chưa có đơn hàng nào được liên kết.</p>
          ) : (
            <ul className="divide-y divide-gray-200">
              {orders.map((order) => {
                const isConflict = Boolean(
                  order.customerName && order.customerName !== billCustomerName
                );
                return (
                  <li key={order.id} className="py-2.5 flex items-center justify-between text-sm">
                    <div>
                      <span className="font-mono font-medium text-gray-900">{order.orderNo}</span>
                      {order.customerName && (
                        <span
                          className={`ml-2 text-xs px-2 py-0.5 rounded font-normal ${
                            isConflict
                              ? "bg-amber-100 text-amber-800 border border-amber-300 font-semibold"
                              : "bg-gray-100 text-gray-700"
                          }`}
                        >
                          {order.customerName} {isConflict ? "(Lệch khách hàng)" : ""}
                        </span>
                      )}
                    </div>
                    <button
                      type="button"
                      data-testid="unlink-order-btn"
                      disabled={order.hasFinancialDependencies}
                      className={`text-xs px-2 py-1 rounded border transition ${
                        order.hasFinancialDependencies
                          ? "opacity-40 cursor-not-allowed bg-gray-100 text-gray-400"
                          : "text-red-600 border-red-300 hover:bg-red-50"
                      }`}
                      onClick={() => {
                        if (order.hasFinancialDependencies) {
                          setWarning(
                            "Cảnh báo bảo toàn lịch sử: Đơn hàng đã phát sinh chi phí / rating. Không thể gỡ liên kết trực tiếp."
                          );
                        } else if (onRemoveOrder) {
                          onRemoveOrder(order.id);
                        }
                      }}
                    >
                      Gỡ
                    </button>
                  </li>
                );
              })}
            </ul>
          )}
        </div>

        {/* Shipments Box */}
        <div className="border rounded-lg p-4 bg-slate-50/50">
          <div className="flex justify-between items-center mb-3">
            <h4 className="font-semibold text-gray-800 text-sm">
              Lô hàng vận chuyển ({shipments.length})
            </h4>
          </div>

          {availableShipments.length > 0 && (
            <div className="flex gap-2 mb-3">
              <label htmlFor="shipment-select" className="sr-only">Thêm lô hàng</label>
              <select
                id="shipment-select"
                aria-label="Thêm lô hàng"
                className="text-xs border rounded p-1.5 flex-1 bg-white"
                value={selectedShipmentToAdd}
                onChange={(e) => setSelectedShipmentToAdd(e.target.value)}
              >
                <option value="">-- Chọn lô hàng để liên kết --</option>
                {availableShipments.map((s) => (
                  <option key={s.id} value={s.id}>
                    {s.shipmentNo}
                  </option>
                ))}
              </select>
              <button
                type="button"
                className="btn btn-sm text-xs px-2.5 py-1 bg-blue-600 text-white rounded hover:bg-blue-700"
                disabled={!selectedShipmentToAdd}
                onClick={() => {
                  if (selectedShipmentToAdd && onAddShipment) {
                    onAddShipment(selectedShipmentToAdd);
                    setSelectedShipmentToAdd("");
                  }
                }}
              >
                + Liên kết lô
              </button>
            </div>
          )}

          {shipments.length === 0 ? (
            <p className="text-xs text-gray-400 italic py-2">Chưa liên kết lô hàng vận chuyển.</p>
          ) : (
            <ul className="divide-y divide-gray-200">
              {shipments.map((shipment) => (
                <li key={shipment.id} className="py-2.5 flex items-center justify-between text-sm">
                  <div>
                    <span className="font-mono font-medium text-gray-900">
                      {shipment.shipmentNo}
                    </span>
                    {shipment.hasRatingDependency && (
                      <span className="ml-2 text-xs px-2 py-0.5 rounded bg-amber-100 text-amber-800">
                        Có phát sinh chi phí
                      </span>
                    )}
                  </div>
                  <button
                    type="button"
                    data-testid="unlink-shipment-btn"
                    disabled={shipment.hasRatingDependency}
                    className={`text-xs px-2 py-1 rounded border transition ${
                      shipment.hasRatingDependency
                        ? "opacity-40 cursor-not-allowed bg-gray-100 text-gray-400"
                        : "text-red-600 border-red-300 hover:bg-red-50"
                    }`}
                    onClick={() => {
                      if (shipment.hasRatingDependency) {
                        setWarning(
                          "Cảnh báo bảo toàn lịch sử: Lô hàng/Chặng đã phát sinh Rating hoặc Chi phí. Không thể gỡ liên kết trực tiếp để tránh mất dấu vết tài chính."
                        );
                      } else if (onRemoveShipment) {
                        onRemoveShipment(shipment.id);
                      }
                    }}
                  >
                    Gỡ
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>
      </div>
    </div>
  );
}
