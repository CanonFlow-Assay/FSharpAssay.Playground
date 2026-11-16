namespace ColdProof.Domain

open System

module batchesDomain

open Canon.Core

type storage_class =
    | FROZEN
    | COLD
    | COOL
    | AMBIENT

// Projected from id
type id = private id of System.Guid
// Projected from batch_no
type batch_no = private batch_no of string
// Projected from drug_name
type drug_name = private drug_name of string
// Projected from storage_class
type storage_class = private storage_class of storage_class
// Projected from min_temp
type min_temp = private min_temp of decimal
// Projected from max_temp
type max_temp = private max_temp of decimal
// Projected from received_at
type received_at = private received_at of System.DateTimeOffset
// Projected from expires_at
type expires_at = private expires_at of System.DateTime
// Projected from units
type units = private units of int
// Projected from license_no
type license_no = private license_no of string


module temperature_logsDomain

open Canon.Core

// Projected from id
type id = private id of System.Guid
// Projected from batch_id
type batch_id = private batch_id of System.Guid
// Projected from reading
type reading = private reading of decimal
// Projected from sensor_id
type sensor_id = private sensor_id of string
// Projected from recorded_at
type recorded_at = private recorded_at of System.DateTimeOffset


module shipmentsDomain

open Canon.Core

// Projected from id
type id = private id of System.Guid
// Projected from batch_id
type batch_id = private batch_id of System.Guid
// Projected from status
type status = private status of string
// Projected from quantity
type quantity = private quantity of int
// Projected from destination
type destination = private destination of string
// Projected from total_value
type total_value = private total_value of decimal
// Projected from gst
type gst = private gst of decimal
// Projected from discount
type discount = private discount of decimal
// Projected from invoice_total
type invoice_total = private invoice_total of decimal
// Projected from spoilage_units
type spoilage_units = private spoilage_units of int
// Projected from inspector_notes
type inspector_notes = private inspector_notes of string
// Projected from metadata
type metadata = private metadata of string
// Projected from created_at
type created_at = private created_at of System.DateTimeOffset
// Projected from released_at
type released_at = private released_at of System.DateTimeOffset
// Projected from shipped_at
type shipped_at = private shipped_at of System.DateTimeOffset
// Projected from delivered_at
type delivered_at = private delivered_at of System.DateTimeOffset
