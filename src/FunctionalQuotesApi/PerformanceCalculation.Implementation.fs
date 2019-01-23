module FunctionalQuotesApi.PerformanceCalculation.Implementation

open FunctionalQuotesApi.Common

let calculatePerformance : CalculatePerformance =
    fun date maxTimeThreshold quotes ->
        let calculateWithPivot pivot =
            let quoteIsPast (q: Quote) = q.Date < date.Date
            let quoteDateIsWithinThreshold (q: Quote) = q.Date - date <= maxTimeThreshold
            
            quotes
            |> Seq.skipWhile quoteIsPast
            |> Seq.tryHead
            |> Option.filter quoteDateIsWithinThreshold
            |> Option.map (fun x -> {
                Date = x.Date;
                Variation = (pivot.Value - x.Value) / x.Value * 100.0
            })
            
        let last = List.tryLast quotes
        last |> Option.bind calculateWithPivot