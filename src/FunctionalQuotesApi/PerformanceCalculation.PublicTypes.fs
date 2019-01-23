namespace FunctionalQuotesApi.PerformanceCalculation

open System
open FunctionalQuotesApi.Common

// quote list assumed to be sorted
type CalculatePerformance = DateTime -> TimeSpan -> Quote list -> Performance option