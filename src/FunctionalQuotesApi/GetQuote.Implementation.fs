module FunctionalQuotesApi.GetQuote.Implementation

open System
open System.Net.Http
open FunctionalQuotesApi.Common
open FunctionalQuotesApi.GetQuote

let requestMorningstar (client: HttpClient) (date: DateTime) (quoteId: QuoteId) : AsyncResult<string, Exception> =
    let baseQuery = "?id={0}%5D22%5D1%5D" +
                    "currencyId=EUR&" +
                    "idtype=Morningstar&" +
                    "priceType=&" +
                    "frequency=daily&" +
                    "startDate={1}&" +
                    "endDate={2}&" +
                    "outputType=COMPACTJSON"
    let _from = date.AddYears(-5).ToString("yyyy-MM-dd")
    let _to = date.ToString("yyyy-MM-dd")
    let queryString = String.Format(baseQuery, (QuoteId.value quoteId), _from, _to)

    async {
        let uri = new Uri(queryString, UriKind.Relative)
        try
            let! response = client.GetAsync(uri) |> Async.AwaitTask
            let! responseContent = response.Content.ReadAsStringAsync() |> Async.AwaitTask
            return Ok responseContent
        with 
        //| :? HttpRequestException as ex -> return Error (ex :> Exception)
        | ex -> return Error ex
    }

let deserializeQuotes content = 
    result {
        let! payload = Json.deserialize<string[][]> content
        return payload |> Array.map (fun x -> { 
                Date = DateTimeOffset.FromUnixTimeMilliseconds(Int64.Parse(x.[0])).Date
                Value = Double.Parse(x.[1], Globalization.NumberStyles.AllowDecimalPoint)
            })
    }

let getQuotes 
    httpClient
    date : GetQuotes =
    fun quoteId -> 
        asyncResult {
            let! response =
                requestMorningstar httpClient date quoteId 
                |> AsyncResult.mapError GetQuotesError.MorningstarRequest
            let! quotes = 
                deserializeQuotes response 
                |> Async.retn 
                |> AsyncResult.mapError GetQuotesError.Deserialization
            return quotes
                |> Array.sortBy (fun (q:Quote) -> q.Date)
                |> List.ofArray
         }