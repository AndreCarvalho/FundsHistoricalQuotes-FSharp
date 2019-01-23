namespace FunctionalQuotesApi.GetQuote

open System
open FunctionalQuotesApi.Common

type MorningstarRequestError = MorningstarRequestError of Exception
  
type GetQuotesError =
  | MorningstarRequest of Exception
  | Deserialization of Exception

type GetQuotes = QuoteId -> AsyncResult<Quote list, GetQuotesError>