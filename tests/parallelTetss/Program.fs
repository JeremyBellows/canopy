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
    [0..amount] |> List.iter(fun index -> 
        context <| sprintf "Suite %i" index
        "Test One" &&& fun browser -> 
            let browser = browser.Value   
            testpage |> __url browser
            __equals browser "#welcome" "Welcome"

        "Test Two" &&& fun browser ->    
            let browser = browser.Value   
            testpage |> __url browser
            __equals browser "#welcome" "Welcome"
        )

addSuites 20
runParallel ()
System.Console.ReadLine() |> ignore
