#!/bin/sh
dotnet restore src/FunctionalQuotesApi
dotnet build src/FunctionalQuotesApi

dotnet restore tests/FunctionalQuotesApi.Tests
dotnet build tests/FunctionalQuotesApi.Tests
dotnet test tests/FunctionalQuotesApi.Tests
