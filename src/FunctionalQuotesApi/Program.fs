module FunctionalQuotesApi.App

open System
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Cors.Infrastructure
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open System.Net.Http
open Newtonsoft.Json
open Newtonsoft.Json.Serialization
open Giraffe
open Giraffe.Serialization.Json
open FunctionalQuotesApi.HttpHandlers
open FunctionalQuotesApi.Infrastructure

// ---------------------------------
// Web app
// ---------------------------------

let client = new HttpClient()

let throttler = new Throttler()
let time = 
    fun () -> DateTime.Now

let webApp =
    choose [
        subRoute "/api"
            (choose [
                GET >=> choose [
                    route "/hello" >=> handleGetHello
                    routef "/quotes/%s/last" (handleGetLastQuote client throttler (time())) // TODO: warble
                    routef "/quotes/%s/performances" (handleGetPerformances client throttler (time())) // TODO: warble
                    routef "/quotes/%s" (handleGetQuotes client throttler (time())) // TODO: warble
                ]
            ])
        setStatusCode 404 >=> text "Not Found" ]

// ---------------------------------
// Error handler
// ---------------------------------

let errorHandler (ex : Exception) (logger : ILogger) =
    logger.LogError(ex, "An unhandled exception has occurred while executing the request.")
    clearResponse >=> setStatusCode 500 >=> text ex.Message

// ---------------------------------
// Config and Main
// ---------------------------------

let configureHttpClient (client: HttpClient) =
    client.BaseAddress <- new Uri("http://tools.morningstar.es/api/rest.svc/timeseries_price/2nhcdckzon") 
    ()

let configureCors (builder : CorsPolicyBuilder) =
    builder.WithOrigins("http://localhost:8080")
           .AllowAnyMethod()
           .AllowAnyHeader()
           |> ignore

let configureApp (app : IApplicationBuilder) =
    configureHttpClient client
    let env = app.ApplicationServices.GetService<IHostingEnvironment>()
    (match env.IsDevelopment() with
    | true  -> app.UseDeveloperExceptionPage()
    | false -> app.UseGiraffeErrorHandler errorHandler)
        .UseHttpsRedirection()
        .UseCors(configureCors)
        .UseGiraffe(webApp)

let configureServices (services : IServiceCollection) =
    let customSettings = JsonSerializerSettings(DateFormatString = "yyyy-MM-dd", ContractResolver = CamelCasePropertyNamesContractResolver())
    services.AddSingleton<IJsonSerializer>(NewtonsoftJsonSerializer(customSettings)) |> ignore
    services.AddCors()    |> ignore
    services.AddGiraffe() |> ignore
    services.AddMemoryCache() |> ignore

let configureLogging (builder : ILoggingBuilder) =
    builder//.AddFilter(fun l -> l.Equals LogLevel.Error)
           .AddConsole()
           .AddDebug() |> ignore

[<EntryPoint>]
let main _ =
    WebHostBuilder()
        .UseKestrel()
        .UseIISIntegration()
        .Configure(Action<IApplicationBuilder> configureApp)
        .ConfigureServices(configureServices)
        .ConfigureLogging(configureLogging)
        .Build()
        .Run()
    0