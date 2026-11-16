module Playground.Acceptance.Program

open System
open System.Text.Json
open Expecto

module W = WebuyCore.Domain.ValueObjects
module WM = WebuyCore.Domain.DomainModel
module WS = WebuyCore.Domain.StateMachines
module WE = WebuyCore.Domain.Enums
module WB = WebuyCore.Domain.ONDCBehavior

let value = function Ok x -> x | Error e -> failwithf "invalid fixture: %A" e
let count (item: 'a) =
    let _,fields = Microsoft.FSharp.Reflection.FSharpValue.GetUnionFields(box item, typeof<'a>, System.Reflection.BindingFlags.Public ||| System.Reflection.BindingFlags.NonPublic)
    unbox<int> fields.[0]
let now = DateTimeOffset(2026,1,10,12,0,0,TimeSpan.Zero)
let origin = W.GeoCoord.create 0.0 0.0 |> value
let order status : WM.Order = {
    Id=Guid.Empty; ONDC_txn_id=W.TransactionId Guid.Empty; ONDC_message_id=W.MessageId Guid.Empty
    ConsumerId=Guid.Empty; StoreId=Guid.Empty; StoreLocation=origin; PartnerId=None; Items=[]
    SubTotal=W.Money.create 100m |> value; DeliveryFee=W.Money.create 0m |> value
    SurgeFee=W.Money.create 0m |> value; Discount=W.Money.create 0m |> value; TotalFare=W.Money.create 100m |> value
    PaymentMethod=WE.PaymentMethod.UPI; PaymentStatus=WE.PaymentStatus.Initiated; Status=status
    OTP=W.OTP.create "1234" |> value; DeliveryAddress="fixture"; DeliveryLocation=origin
    DeliveryETA=W.DeliveryETA.create 30 |> value; Distance=W.Distance.create 1.0 |> value
    CreatedAt=now; ConfirmedAt=None; PackedAt=None; PickedUpAt=None; InTransitAt=None; DeliveredAt=None; CancelledAt=None; CancellationReason=None
}
let inventory () : WM.InventoryItem = {
    ProductId=Guid.Empty; StoreId=Guid.Empty
    Stock=W.StockCount.create 5 |> value; Reserved=W.StockCount.create 1 |> value; Available=W.StockCount.create 4 |> value
    ShelfLocation="A"; ExpiryBatch=Some(W.ExpiryDate.create (now.Date.AddDays(-1.)) (now.Date.AddDays(-2.)) |> value)
    StorageTemp=None; LastRestocked=now.AddDays(-2.); Status=WE.InventoryStatus.InStock
}

[<Tests>]
let tests = testList "Imported enterprise acceptance" [
    for bad in [Double.NaN; Double.PositiveInfinity; Double.NegativeInfinity] do
        testCase (sprintf "coordinates reject nonfinite %A" bad) <| fun _ ->
            Expect.isError (W.GeoCoord.create bad 0.) "latitude must be finite"
            Expect.isError (W.GeoCoord.create 0. bad) "longitude must be finite"
        testCase (sprintf "distance rejects nonfinite %A" bad) <| fun _ -> Expect.isError (W.Distance.create bad) "distance must be finite"
        testCase (sprintf "discount rejects nonfinite %A" bad) <| fun _ -> Expect.isError (W.DiscountPct.create bad) "discount must be finite"
        testCase (sprintf "radius rejects nonfinite %A" bad) <| fun _ -> Expect.isError (W.DeliveryRadius.create bad) "radius must be finite"
    testCase "identifier fixture fits both domain and fifteen-character SQL field" <| fun _ ->
        Expect.isOk (W.GSTIN.create "22AAAAA0000A1Z5") "shared fifteen-character fixture"
        Expect.isError (W.GSTIN.create "22AAAAA0000A11Z5") "sixteen characters exceed the schema field"
    testCase "same location has zero distance" <| fun _ -> Expect.floatClose Accuracy.high (WebuyCore.Domain.DistanceUtil.calculate origin origin) 0. "same point"
    testCase "one degree is more than one hundred kilometres" <| fun _ ->
        let far=W.GeoCoord.create 1. 0. |> value
        Expect.isGreaterThan (WebuyCore.Domain.DistanceUtil.calculate origin far) 100. "geofence cannot use a constant mock"
    testCase "far partner cannot deliver an in-transit order" <| fun _ ->
        let far=W.GeoCoord.create 1. 0. |> value
        Expect.isError (WS.OrderStateMachine.onDeliver (order WE.OrderStatus.InTransit) far now) "outside 100m"
    testCase "near partner can deliver" <| fun _ ->
        let result=WS.OrderStateMachine.onDeliver (order WE.OrderStatus.InTransit) origin now |> value
        Expect.equal result.Status WE.OrderStatus.Delivered "valid transition"
    testCase "expiry quarantines inventory without constructing invalid quantity" <| fun _ ->
        let initial=inventory()
        let expired=WS.InventoryStateMachine.checkExpiry initial now |> value
        Expect.equal expired.Status WE.InventoryStatus.Expired "expired status"
        Expect.equal (count expired.Available) 0 "expired stock cannot be available"
        Expect.equal expired.Stock initial.Stock "physical stock retained for reconciliation"
        Expect.equal expired.Reserved initial.Reserved "reservations retained for explicit resolution"
    testCase "future cancellation is rejected" <| fun _ ->
        Expect.isError (WS.OrderStateMachine.onCancel (order WE.OrderStatus.Created) (now.AddSeconds(-1.))) "time cannot precede creation"
    testCase "wrong pickup OTP is rejected" <| fun _ -> Expect.isError (WS.OrderStateMachine.onPickup (order WE.OrderStatus.Packed) "0000" now) "OTP boundary"
    testCase "created order cannot be delivered directly" <| fun _ -> Expect.isError (WS.OrderStateMachine.onDeliver (order WE.OrderStatus.Created) origin now) "transition boundary"
    testCase "signature verification fails closed without a verifier" <| fun _ ->
        Expect.isError (WB.verifySignature "payload" "nonempty-fake-signature" "nonempty-fake-key") "nonempty strings are not cryptographic evidence"
    testCase "NACK error text remains JSON data" <| fun _ ->
        use parsed=JsonDocument.Parse(WB.handleAckNack (Error "quote\" and newline\n" : Result<unit,string>))
        Expect.equal (parsed.RootElement.GetProperty("error").GetProperty("message").GetString()) "quote\" and newline\n" "proper escaping"
    testCase "JSON null is rejected at the typed boundary" <| fun _ ->
        Expect.isError (WB.parseMessage<WebuyCore.Domain.BecknProtocol.SearchRequest> "null") "null is not a valid request"
    testCase "wallet rejects a negative debit" <| fun _ ->
        let balance : FintechWallet.Domain.WalletBalance = { WalletId=Guid.Empty;AvailableBalance=10m;LockedBalance=0m;UpdatedAt=now }
        Expect.isFalse (FintechWallet.Domain.WalletRules.canDebit balance -1m) "negative debit must not credit funds"
    testCase "wallet rejects a zero debit" <| fun _ ->
        let balance : FintechWallet.Domain.WalletBalance = { WalletId=Guid.Empty;AvailableBalance=10m;LockedBalance=0m;UpdatedAt=now }
        Expect.isFalse (FintechWallet.Domain.WalletRules.canDebit balance 0m) "zero is not a debit"
    testCase "wallet allows exact balance debit" <| fun _ ->
        let balance : FintechWallet.Domain.WalletBalance = { WalletId=Guid.Empty;AvailableBalance=10m;LockedBalance=0m;UpdatedAt=now }
        Expect.isTrue (FintechWallet.Domain.WalletRules.canDebit balance 10m) "inclusive funds boundary"
    testCase "airport null produces expected error" <| fun _ -> Expect.isError (AirlineCore.Domain.AirportCode.create null) "external null is rejected"
    testCase "banking positive amount rejects zero" <| fun _ -> Expect.isError (BankingCore.Domain.ValueObjects.PositiveAmount.create 0m) "positive amount invariant"
    testCase "hospital cannot register an existing patient" <| fun _ ->
        let dob=HospitalCore.Domain.DateOfBirth.create (DateTime(2000,1,1)) |> value
        let patient : HospitalCore.Domain.Patient = {Id=HospitalCore.Domain.PatientId Guid.Empty;FirstName="A";LastName="B";Dob=dob;Gender=HospitalCore.Domain.Other;Status=HospitalCore.Domain.Registered}
        Expect.isError (HospitalCore.Domain.PatientBehavior.execute (HospitalCore.Domain.PatientBehavior.Register("A","B",DateTime(2000,1,1),HospitalCore.Domain.Other)) (Some patient)) "closed command/state coverage"
    testCase "chat rejects read before send" <| fun _ ->
        let message : WecarCore.Domain.DomainModel.Message = {
            Id=WecarCore.Domain.DomainModel.MessageId Guid.Empty;SenderId=WecarCore.Domain.DomainModel.UserId Guid.Empty
            Target=WecarCore.Domain.DomainModel.DirectMessage(WecarCore.Domain.DomainModel.UserId Guid.Empty)
            Content=WecarCore.Domain.ValueObjects.MessageContent.create "hello" |> value;Type=WecarCore.Domain.DomainModel.Text;SentAt=now;ReadAt=None }
        Expect.isError (WecarCore.Domain.ChatBehavior.markRead message (now.AddSeconds(-1.))) "temporal invariant"
]
