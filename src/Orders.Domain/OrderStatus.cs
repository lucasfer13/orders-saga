namespace Orders.Domain;

/// <summary>
/// Estado del agregado Order. Deliberadamente más simple que el state
/// machine de la saga (docs/BACKLOG.md, T18): aquí solo importa qué le ha
/// pasado de verdad al pedido, no el detalle de orquestación entre servicios.
/// </summary>
public enum OrderStatus
{
    Pending,
    StockReserved,
    PaymentCharged,
    Shipped,
    Compensating,
    Cancelled,
}
