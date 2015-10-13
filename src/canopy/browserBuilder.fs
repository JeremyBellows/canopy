module canopy.builders

type browserBuilder() =
  let mutable browserInstance : OpenQA.Selenium.IWebDriver = null
  let browserFunction action = browserInstance |> action

  member this.Bind(action, actionFunction) =
    browserFunction <| actionFunction action

  member this.Return(x) =
    x
