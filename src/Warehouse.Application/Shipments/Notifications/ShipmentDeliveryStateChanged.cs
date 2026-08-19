namespace Warehouse.Application.Shipments.Notifications;

using MediatR;
using System;
using Warehouse.Domain;

/// <summary>
/// Published after a delivery-state change has been persisted. Anything that should happen
/// as a consequence of the move -- today, notifying the supplier -- subscribes to this rather
/// than being called inline by the command handler.
/// </summary>
public record ShipmentDeliveryStateChanged(
    Guid ShipmentId,
    string ReferenceNumber,
    ShipmentStatus PreviousStatus,
    ShipmentStatus NewStatus) : INotification;
