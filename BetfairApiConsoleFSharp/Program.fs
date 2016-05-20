// Bfexplorer cannot be held responsible for any losses or damages incurred during the use of this betfair bot.
// It is up to you to determine the level of risk you wish to trade under. 
// Do not gamble with money you cannot afford to lose.

open System
open System.Diagnostics

open BeloSoft.Data
open BeloSoft.Betfair.API
open BeloSoft.Betfair.API.Models

// Timer helpers
let timer = Stopwatch()
    
let startTimeMeasure (message : string) =
    Console.Write(sprintf "\n%s" message)

    if timer.ElapsedTicks <> 0L
    then
        timer.Reset()

    timer.Start()

let stopTimeMeasure() =
    timer.Stop()

    Console.WriteLine(sprintf " %dms\n" timer.ElapsedMilliseconds)

[<EntryPoint>]
let main argv =
    if argv.Length <> 2
    then
        failwith "Please enter your betfair user name and password!"        
       
    // Login       
    let username, password = argv.[0], argv.[1]

    let betfairServiceProvider = BetfairServiceProvider(BetfairApiServer.GBR)

    let accountOperations = betfairServiceProvider.AccountOperations
    let browsingOperations = betfairServiceProvider.BrowsingOperations

    // Get market books (market selections prices)
    let getMarketBooks(marketCatalogue : MarketCatalogue) = async {

        startTimeMeasure "GetMarketBooks ..."

        let! result = browsingOperations.GetMarketBooks([| marketCatalogue.marketId |], priceProjection = PriceProjection.DefaultActiveMarket())

        stopTimeMeasure()

        if result.IsSuccessResult
        then
            let marketBook = result.SuccessResult.[0]
            let betEvent = marketCatalogue.event

            Console.WriteLine(sprintf "%A: %s" betEvent.openDate betEvent.name)
                    
            Seq.iter2 (fun (runner : RunnerCatalog) (runnerData : Runner) ->
                    Console.WriteLine(sprintf "%s: %.2f" runner.runnerName runnerData.lastPriceTraded)
                )
                marketCatalogue.runners marketBook.runners
    }

    // Place bet
    let placeBet(marketId, selectionId, side, size, price) = async {
        
        startTimeMeasure "PlaceOrders ..."

        (*
        let instructions = [| PlaceOrderInstruction.LimitOrder(selectionId, side, size, price) |]

        let! result = betfairServiceProvider.BettingOperations.PlaceOrders(marketId, instructions)
        *)

        let! result = betfairServiceProvider.BettingOperations.PlaceOrder(marketId, selectionId, side, size, price)

        stopTimeMeasure()

        if result.IsSuccessResult
        then
            let placeExecutionReport = result.SuccessResult

            Console.WriteLine(sprintf "Bet ID: %s" placeExecutionReport.instructionReports.[0].betId)
    }

    // Cancel bets
    let cancelBets marketId = async {
        startTimeMeasure "CancelOrders ..."

        let! result = betfairServiceProvider.BettingOperations.CancelOrders(marketId)

        stopTimeMeasure()

        if result.IsSuccessResult
        then
            result.SuccessResult.instructionReports
            |> Seq.iter (fun instructionReport -> 
                    Console.WriteLine(sprintf "Cancelled Bet ID: %s" instructionReport.instruction.betId)
                )
    }

    // Test
    async {   
    
        startTimeMeasure "Login ..."
             
        let! loginResult = accountOperations.Login(username, password)        

        stopTimeMeasure()

        if loginResult.IsSuccessResult
        then
            let today = DateTime.Today

            let filter = 
                createMarketFilterParameters()
                |> withMarketFilterParameter (MarketStartTime (TimeRange.FromRange(today.AddDays(1.0), today.AddDays(2.0))))
                |> withMarketFilterParameter (MarketCountries [| "GB" |])
                |> withMarketFilterParameter (EventTypeIds [| 1 |])
                |> withMarketFilterParameter (MarketTypeCodes [| "MATCH_ODDS" |])
                |> withMarketFilterParameter (InPlayOnly false)

            let marketProjection = [| 
                    MarketProjection.EVENT
                    MarketProjection.MARKET_START_TIME
                    MarketProjection.COMPETITION
                    MarketProjection.RUNNER_DESCRIPTION
                    MarketProjection.MARKET_DESCRIPTION |]
            
            startTimeMeasure "GetMarketCatalogues ..."

            let! marketCataloguesResult = browsingOperations.GetMarketCatalogues(filter, 1, marketProjection, MarketSort.MAXIMUM_TRADED)

            stopTimeMeasure()

            if marketCataloguesResult.IsSuccessResult
            then
                let marketCatalogues = marketCataloguesResult.SuccessResult

                marketCatalogues
                |> Seq.iter (fun marketCatalogue -> 
                        let betEvent = marketCatalogue.event

                        Console.WriteLine(sprintf "%A: %s, eventId: %s, marketId: %s" betEvent.openDate betEvent.name betEvent.id marketCatalogue.marketId)
                    )

                let marketCatalogue = marketCatalogues |> Seq.head

                let mutable i = 0

                while i < 10 do
                    do! getMarketBooks marketCatalogue
                    do! Async.Sleep 50
                    i <- i + 1

                i <- 0

                // Bet operations
                let marketId = marketCatalogue.marketId
                let selectionId = marketCatalogue.runners.[0].selectionId

                while i < 20 do
                    do! placeBet(marketId, selectionId, Side.BACK, 2.0, 1000.0)
                    //do! Async.Sleep 2000
                    i <- i + 1

                do! cancelBets marketId

            startTimeMeasure "Logout ..."

            do! accountOperations.Logout() |> Async.Ignore

            stopTimeMeasure()
    }
    |> Async.RunSynchronously
    
    Console.WriteLine("End of the test.")
    Console.Read() |> ignore
    0 // return an integer exit code