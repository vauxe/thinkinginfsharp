namespace Booking.Domain

open System
open System.Threading
open System.Threading.Tasks
open Booking.Domain.Workflow

module Ports =
    type PaymentRequest =
        { RequestId: RequestId
          Seats: SeatCount }

    type PaymentOutcome =
        | Authorized of transactionId: string
        | Declined of reason: string

    type NotificationRequest =
        { RequestId: RequestId
          Message: string }

    type AsyncPorts =
        {
            LoadBooking: RequestId -> CancellationToken -> Task<BookingState>
            AppendEvent: RequestId -> BookingEvent -> CancellationToken -> Task<unit>
            Charge: PaymentRequest -> CancellationToken -> Task<PaymentOutcome>
            Notify: NotificationRequest -> CancellationToken -> Task<unit>
            GetUtcNow: CancellationToken -> Task<DateTimeOffset>
        }
