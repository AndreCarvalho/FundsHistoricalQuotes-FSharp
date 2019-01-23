namespace FunctionalQuotesApi

module HttpHandlers =

    open System
    open Microsoft.AspNetCore.Http
    open Microsoft.Extensions.Logging
    open FSharp.Control.Tasks.V2.ContextInsensitive
    open Giraffe
    open FunctionalQuotesApi.Models
    open FunctionalQuotesApi.Common
    open FunctionalQuotesApi.GetQuote
    open FunctionalQuotesApi.GetQuote.Implementation
    open FunctionalQuotesApi.PerformanceCalculation.Implementation
    open FSharp.Collections.ParallelSeq
    open Infrastructure
    open Microsoft.Extensions.Caching.Memory
    
    type ApiError = {
        Message: string;
    }
    
    let mapErrorToResponse = 
        fun err -> 
            match err with
                | GetQuotesError.MorningstarRequest ex -> ( { Message = ex.Message }, 500)
                | GetQuotesError.Deserialization ex -> ( { Message = ex.Message }, 500)

    let errorResponse error statusCode : HttpHandler =
        (setStatusCode statusCode >=> json error)
       
    let cacheOp (op: AsyncResult<'a, 'err>) (key: string) (cache: IMemoryCache) : AsyncResult<'a, 'err> =
        async {
            let (isPresent, value) = cache.TryGetValue<'a> key
            if isPresent then
                return Result.Ok value
            else
                let! freshValue = op
                freshValue |> Result.iter (fun quotes -> cache.Set (key, quotes, TimeSpan.FromHours 8.) |> ignore)
                return freshValue
        }
                
    let getFreshQuotes =
        fun httpClient date quoteId ->
            getQuotes httpClient date (QuoteId quoteId)
    
    let getCacheKey quoteId =
        let (QuoteId q) = quoteId
        sprintf "quotes-%s" q
    
    let handleGetHello =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            task {
                let response = {
                    Text = "Hello world, from Giraffe!"
                }
                return! json response next ctx
            }

    let handleGetLastQuote httpClient (throttler: Throttler) date quoteId =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            task {
                let logger = ctx.GetLogger("handleGetLastQuote")
                do logger.LogInformation(quoteId)
                
                let quoteId = QuoteId quoteId
                let cache = ctx.GetService<IMemoryCache>()
                let cacheKey = getCacheKey quoteId
                let getFreshQuotes = throttler.Run(cacheKey, (getQuotes httpClient date quoteId))
                let! result = cacheOp getFreshQuotes cacheKey cache
                
                let lastQuoteResult = result |> Result.map List.tryLast

                return!
                    (match lastQuoteResult with
                        | Ok quoteOption ->
                            match quoteOption with
                                | Some quote -> json quote
                                | None -> errorResponse { Message = "No quotes were retrieved" } 400
                        | Error err ->
                            let (error, code) = mapErrorToResponse err
                            errorResponse error code)
                    next ctx
            }
           
    let handleGetQuotes httpClient (throttler: Throttler) date quoteId =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            task {
                let logger = ctx.GetLogger("handleGetQuotes")
                do logger.LogInformation(quoteId)
                
                let quoteId = QuoteId quoteId
                let cache = ctx.GetService<IMemoryCache>()
                let cacheKey = getCacheKey quoteId
                let getFreshQuotes = throttler.Run(cacheKey, (getQuotes httpClient date quoteId))
                let! result = cacheOp getFreshQuotes cacheKey cache

                return!
                    (match result with
                        | Ok quote -> json quote
                        | Error err ->
                            let (error, code) = mapErrorToResponse err
                            errorResponse error code)
                    next ctx 
            }


    let handleGetPerformances httpClient (throttler: Throttler) date quoteId =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            task {
                let logger = ctx.GetLogger("handleGetPerformances")
                do logger.LogInformation(quoteId)
                
                let quoteId = QuoteId quoteId
                let cache = ctx.GetService<IMemoryCache>()
                let cacheKey = getCacheKey quoteId
                let getFreshQuotes = throttler.Run(cacheKey, (getQuotes httpClient date quoteId))
                let! result = cacheOp getFreshQuotes cacheKey cache
                                
                let periods = [
                    ("1M", date.AddMonths -1);
                    ("3M", date.AddMonths -3);
                    ("6M", date.AddMonths -6);
                    ("1Y", date.AddYears -1);
                    ("3Y", date.AddYears -3);
                    ("5Y", date.AddYears -5);
                ]
                
                let threshold = TimeSpan.FromDays 5.

                let performanceOverPeriod =
                    fun period ->
                        calculatePerformance period threshold
                    
                let performancesOverPeriods =
                    fun quotes ->
                        periods
                        |> PSeq.map (fun (label, date) -> (
                                                              label,
                                                              quotes
                                                                  |> performanceOverPeriod date
                                                                  |> Option.defaultValue { Date = DateTime.MinValue; Variation = Double.NaN; }
                                                          ))
                        |> dict
                                
                let performancesResult = result |> Result.map performancesOverPeriods

                return!
                    (match performancesResult with
                        | Ok performances -> json performances 
                        | Error err ->
                            let (error, code) = mapErrorToResponse err
                            errorResponse error code)
                    next ctx 
            }
