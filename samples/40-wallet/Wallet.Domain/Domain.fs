namespace Playground.Wallet

open System

type Debit = private { Reference: string; Amount: decimal }
type Rejection = InvalidReference | InvalidAmount | Precision | LimitExceeded
module Debit =
    let create limit reference amount =
        if String.IsNullOrWhiteSpace reference || reference.Trim().Length>64 then Error InvalidReference
        elif amount<=0m || amount>999999999999.99m then Error InvalidAmount
        elif Decimal.Round(amount,2)<>amount then Error Precision
        elif amount>limit then Error LimitExceeded
        else Ok {Reference=reference.Trim();Amount=amount}
    let reference debit = debit.Reference
    let amount debit = debit.Amount

type Outcome = Accepted of reference: string * balance: decimal | InsufficientFunds of reference: string | Conflict of reference: string

