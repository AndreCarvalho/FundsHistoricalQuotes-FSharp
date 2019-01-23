namespace FunctionalQuotesApi.Common

open System

type Quote = {
  Date: DateTime;
  Value: double;
}

type QuoteId = QuoteId of string

type Performance = {
  Date: DateTime;
  Variation: double;
}

module QuoteId = 
  let value (QuoteId id) = id