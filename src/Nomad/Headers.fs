namespace Nomad

open System.IO

type Range =
    |StartOnlyRange of string * int64
    |StartEndRanges of string * (int64 * int64) list