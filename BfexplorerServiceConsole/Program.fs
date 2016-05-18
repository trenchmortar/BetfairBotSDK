// Bfexplorer cannot be held responsible for any losses or damages incurred during the use of this betfair bot.
// It is up to you to determine the level of risk you wish to trade under. 
// Do not gamble with money you cannot afford to lose.

open System
open System.Diagnostics

open BeloSoft.Data
open BeloSoft.Bfexplorer.Service
open BeloSoft.Bfexplorer.Domain

let showMarketData (market : Market) =
    Console.WriteLine(sprintf "%s, Status: %s, Total matched: %.2f" (market.ToString()) market.MarketStatusText market.TotalMatched)

    market.Selections
    |> Seq.iter (fun selection -> Console.WriteLine(sprintf "\t%s: %.2f | %.2f" selection.Name selection.LastPriceTraded selection.TotalMatched))

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

    let username, password = argv.[0], argv.[1]

    let bfexplorerService = BfexplorerService()

    async {

        startTimeMeasure "Login ..."

        let! loginResult = bfexplorerService.Login(username, password)

        stopTimeMeasure()

        if loginResult.IsSuccessResult
        then
            let today = DateTime.Today

            let filter = [ 
                    //StartTime (today.AddDays(1.0), today.AddDays(2.0))
                    //Countries [| "GB" |]
                    BetEventTypeIds [| 1 |]
                    MarketTypeCodes [| "MATCH_ODDS" |]
                    //InPlayOnly false
                    InPlayOnly true
                    //TurnInPlayEnabled true
                ]
            
            startTimeMeasure "GetMarketCatalogues ..."

            let! marketCataloguesResult = bfexplorerService.GetMarketCatalogues(filter, 10)

            stopTimeMeasure()

            if marketCataloguesResult.IsSuccessResult
            then
                let marketCatalogues = 
                    marketCataloguesResult.SuccessResult
                    |> Seq.sortByDescending (fun marketCatalogue -> marketCatalogue.MarketInfo.StartTime)
                    |> Seq.toArray

                marketCatalogues
                |> Array.iter (fun marketCatalogue -> 
                        let marketInfo = marketCatalogue.MarketInfo
                        let betEvent = marketInfo.BetEvent

                        Console.WriteLine(sprintf "%A: %s, eventId: %d, marketId: %s" betEvent.OpenTime betEvent.Name betEvent.Id marketInfo.Id)
                    )

                let marketInfo = marketCatalogues.[0].MarketInfo

                startTimeMeasure "GetMarket ..."

                let! marketResult = bfexplorerService.GetMarket(marketInfo)

                stopTimeMeasure()

                if marketResult.IsSuccessResult
                then                    
                    let market = marketResult.SuccessResult

                    showMarketData market

                    let continueLooping = ref true

                    Console.CancelKeyPress.Add (fun _ -> continueLooping := false)

                    while !continueLooping do
                        do! Async.Sleep(50)

                        startTimeMeasure "UpdateMarketBaseData ..."

                        let! result = bfexplorerService.UpdateMarketBaseData(market)

                        stopTimeMeasure()

                        if result.IsSuccessResult && market.IsUpdated
                        then
                            showMarketData market
              
            startTimeMeasure "Logout ..."
                                                  
            do! bfexplorerService.Logout() |> Async.Ignore

            stopTimeMeasure()
    }
    |> Async.RunSynchronously

    0 // return an integer exit code