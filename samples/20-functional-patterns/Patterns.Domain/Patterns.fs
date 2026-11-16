namespace Playground.Patterns

open System

type Quantity = private Quantity of int
type InputError = InvalidQuantity | EmptyCustomer

module Quantity =
    let create n = if n >= 1 && n <= 99 then Ok (Quantity n) else Error InvalidQuantity
    let value (Quantity n) = n

type Order = private { Customer: string; Quantity: Quantity }
module Order =
    let create customer quantity =
        let errors = [ if String.IsNullOrWhiteSpace customer then EmptyCustomer; if Result.isError (Quantity.create quantity) then InvalidQuantity ]
        match errors, Quantity.create quantity with
        | [], Ok q -> Ok {Customer=customer.Trim();Quantity=q}
        | _, _ -> Error errors

[<Measure>] type kg
[<Measure>] type km

type Shipment = Pickup of code: string | Delivery of address: string * weight: decimal<kg>
module Shipping =
    let fee = function
        | Pickup _ -> 0m
        | Delivery(_,weight) -> 5m + decimal weight * 2m

type Menu = Link of label: string * path: string | Group of label: string * Menu list
module Menu =
    let rec links = function Link(label,path) -> [label,path] | Group(_,children) -> children |> List.collect links

type AccountEvent = Credited of decimal | Debited of decimal
module Ledger =
    let evolve balance = function Credited amount -> balance+amount | Debited amount -> balance-amount
    let replay events = List.fold evolve 0m events
    let decide amount balance =
        if amount <= 0m then Error "amount-must-be-positive"
        elif amount > balance then Error "insufficient-funds"
        else Ok (Debited amount)

module Workflow =
    // A capability is a function argument; recording is returned as data.
    let execute (price: Quantity -> decimal) customer quantity =
        match Order.create customer quantity with
        | Error errors -> Error errors
        | Ok order -> Ok (price order.Quantity, ["input-validated";"price-calculated"])

module Parsing =
    let (|PositiveInteger|_|) text =
        match Int32.TryParse(text: string) with true,value when value>0 -> Some value | _ -> None
    let positive text = match text with PositiveInteger n -> Ok n | _ -> Error "positive-integer-required"
