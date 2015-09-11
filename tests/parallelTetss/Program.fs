module main

open System
open OpenQA.Selenium
open canopy
open canopy.guts
open runner
open configuration
open reporters
open types

let testpage = "http://lefthandedgoat.github.io/canopy/testpages/" 

let addSuites amount =
    let addTests amount = 
        [0..amount] |> List.iter(fun index ->
            sprintf "Test %i" index &&& fun browser -> 
                let browser = browser.Value   
                testpage |> __url browser
                __equals browser "#welcome" "Welcome"
                )
    [0..amount] |> List.iter(fun index -> 
        context <| sprintf "Suite %i" index

        addTests 50
        )

addSuites 20
runParallel ()
System.Console.ReadLine() |> ignore
