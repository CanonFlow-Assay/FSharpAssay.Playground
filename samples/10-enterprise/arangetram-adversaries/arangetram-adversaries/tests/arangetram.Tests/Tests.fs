module Tests
open Xunit
open FsCheck
open FsCheck.Xunit
// No generators were emitted for this adversarial fixture. Historical dummy property remains non-authoritative.
[<Property>]
let ``Dummy property`` () = true
