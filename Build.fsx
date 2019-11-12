// --------------------------------------------------------------------------------------
// FAKE build script 
// --------------------------------------------------------------------------------------
#r "paket:
nuget Fake.DotNet.AssemblyInfoFile
nuget Fake.DotNet.MSBuild
nuget Fake.DotNet.Fsi
nuget Fake.IO.FileSystem
nuget Fake.Tools.Git
nuget Fake.Core.Target 
nuget Fake.Core.ReleaseNotes 
nuget FSharp.Formatting //"
#load "./.fake/build.fsx/intellisense.fsx"
open Fake.IO.Globbing.Operators
open Fake.Core.TargetOperators

// --------------------------------------------------------------------------------------
// Project-specific details below
// --------------------------------------------------------------------------------------

// Information about the project are used
//  - for version and project name in generated AssemblyInfo file
//  - by the generated NuGet package 
//  - to run tests and to publish documentation on GitHub gh-pages
//  - for documentation, you also need to edit info in "docs/tools/generate.fsx"

// The name of the project 
// (used by attributes in AssemblyInfo, name of a NuGet package and directory in 'src')
let project = "canopy"
let projectIntegration = "canopy.integration"

// Short summary of the project
// (used as description in AssemblyInfo and as a short summary for NuGet package)
let summary = "F# web testing framework"

// Longer description of the project
// (used as a description for NuGet package; line breaks are automatically cleaned up)
let description = """A simple framework in F# on top of selenium for writing UI automation and tests."""
let descriptionIntegration = """A sister package to canopy for integration tests."""
// List of author names (for NuGet package)
let authors = [ "Chris Holt" ]
// Tags for your project (for NuGet package)
let tags = "f# fsharp canopy selenium ui automation tests"

// File system information 
// (<solutionFile>.sln is built during the building process)
let solutionFile  = "canopy"
// Pattern specifying assemblies to be tested using NUnit
let testAssemblies = "tests/**/bin/Release/*basictests*.exe"

// Git configuration (used for publishing documentation in gh-pages branch)
// The profile where the project is posted 
let gitHome = "https://github.com/lefthandedgoat"
// The name of the project on GitHub
let gitName = "canopy"

// --------------------------------------------------------------------------------------
// END TODO: The rest of the file includes standard build steps 
// --------------------------------------------------------------------------------------

// Read additional information from the release notes document
let release = Fake.Core.ReleaseNotes.parse (Fake.IO.File.read "RELEASE_NOTES.md")

// Generate assembly info files with the right version & up-to-date information
Fake.Core.Target.create "AssemblyInfo" (fun _ ->
  let fileName = "src/" + project + "/AssemblyInfo.fs"
  Fake.DotNet.AssemblyInfoFile.createFSharp fileName
      [ Fake.DotNet.AssemblyInfo.Title project
        Fake.DotNet.AssemblyInfo.Product project
        Fake.DotNet.AssemblyInfo.Description summary
        Fake.DotNet.AssemblyInfo.Version release.AssemblyVersion
        Fake.DotNet.AssemblyInfo.FileVersion release.AssemblyVersion ] 

  let fileName = "src/" + projectIntegration + "/AssemblyInfo.fs"
  Fake.DotNet.AssemblyInfoFile.createFSharp fileName
      [ Fake.DotNet.AssemblyInfo.Title projectIntegration
        Fake.DotNet.AssemblyInfo.Product projectIntegration
        Fake.DotNet.AssemblyInfo.Description summary
        Fake.DotNet.AssemblyInfo.Version release.AssemblyVersion
        Fake.DotNet.AssemblyInfo.FileVersion release.AssemblyVersion ] 
)

// --------------------------------------------------------------------------------------
// Clean build results

Fake.Core.Target.create "Clean" (fun _ ->
    Fake.IO.Shell.cleanDirs ["bin"; "temp"]
)

Fake.Core.Target.create "CleanDocs" (fun _ ->
    Fake.IO.Shell.cleanDirs ["docs/output"]
)

// --------------------------------------------------------------------------------------
// Build library & test project

Fake.Core.Target.create "Build" (fun _ ->
    !! (solutionFile + "*.sln")
    |> Fake.DotNet.MSBuild.run id "" "Rebuild" [ "Configuration", "Release"; "VisualStudioVersion", "15.0" ]
    |> ignore
)

// --------------------------------------------------------------------------------------
// Run the unit tests using test runner

Fake.Core.Target.create "RunTests" (fun _ ->
    !! testAssemblies 
    |> Seq.iter (fun testFile ->
      Fake.Core.Command.ShellCommand(testFile)
      |> Fake.Core.CreateProcess.fromCommand
      |> Fake.Core.CreateProcess.ensureExitCodeWithMessage("Basic Tests failed")
      |> Fake.Core.Proc.run
      |> ignore
    )
)

// --------------------------------------------------------------------------------------
// Generate the documentation

// --------------------------------------------------------------------------------------
// Builds the documentation from `.fsx` and `.md` files in the 'docs/content' directory
// (the generated documentation is stored in the 'docs/output' directory)
// --------------------------------------------------------------------------------------

// Binaries that have XML documentation (in a corresponding generated XML file)
let referenceBinaries = [ "canopy.dll" ]
    //[ "WebDriver.dll"; "WebDriver.Support.dll"; "Newtonsoft.Json.dll"; "SizSelCsZzz.dll"; "canopy.dll" ]
// Web site location for the generated documentation
let website = "/canopy"

let githubLink = "https://github.com/lefthandedgoat/canopy"

// Specify more information about your project
let info =
  [ "project-name", "canopy"
    "project-author", "Chris Holt"
    "project-summary", "A simple framework in f# on top of selenium for writing UI automation and tests."
    "project-github", githubLink
    "project-nuget", "https://www.nuget.org/packages/canopy/" ]

// When called from 'build.fsx', use the public project URL as <root>
// otherwise, use the current 'output' directory.
#if RELEASE
let root = website
#else
let root = "file://" + (__SOURCE_DIRECTORY__ + "/docs/output")
#endif

// Paths with template/source/output locations
let bin        = __SOURCE_DIRECTORY__ + "/src/canopy/bin/Release/netstandard2.0"
let content    = __SOURCE_DIRECTORY__ + "/docs/content"
let output     = __SOURCE_DIRECTORY__ + "/docs/output"
let files      = __SOURCE_DIRECTORY__ + "/docs/files"
let templates  = __SOURCE_DIRECTORY__ + "/docs/tools/templates"
let formatting = __SOURCE_DIRECTORY__ + "/packages/FSharp.Formatting"
let docTemplate = formatting + "/templates/docpage.cshtml"

// Where to look for *.csproj templates (in this order)
let layoutRoots =
  [ templates; formatting + "templates"
    formatting + "templates/reference" ]

// Copy static files and CSS + JS from F# Formatting
let copyFiles () =
  Fake.IO.Shell.copyRecursive files output true |> ignore 
  // |> Log "Copying file: "
  //TODO: I'm ignoring output for now until I can find a replacement for the original Fake Log function
  Fake.IO.Directory.ensure (output + "/content")
  Fake.IO.Shell.copyRecursive (formatting + "/styles") (output + "/content") true 
  |> ignore
    //|> Log "Copying styles and scripts: "

// Build API reference from XML comments
let buildReference () =
  Fake.IO.Shell.cleanDir (output + "/reference")
  let parameters = ("root", root)::info 
  let sourceRepo = githubLink + "/tree/scaffold" // TODO: revert to "tree/master"
  for lib in referenceBinaries do
    FSharp.MetadataFormat.MetadataFormat.Generate
      ( 
      dllFile = bin + "/" + lib, 
      outDir = output + "/reference", 
      layoutRoots = layoutRoots, 
      parameters = parameters,
      sourceRepo = sourceRepo,
      sourceFolder = __SOURCE_DIRECTORY__,
      publicOnly = true,
      libDirs = [__SOURCE_DIRECTORY__ + "/bin"]
      )

// Build documentation from `fsx` and `md` files in `docs/content`
let buildDocumentation () =
  let subdirs = System.IO.Directory.EnumerateDirectories(content, "*", System.IO.SearchOption.AllDirectories)
  for dir in Seq.append [content] subdirs do
    let sub = if dir.Length > content.Length then dir.Substring(content.Length + 1) else "."
    FSharp.Literate.Literate.ProcessDirectory
      ( dir, docTemplate, output + "/" + sub, replacements = ("root", root)::info,
        layoutRoots = layoutRoots )

// Generate
let generateDocs () =
  copyFiles()
  buildDocumentation()
  buildReference()


/// Run the given buildscript with FAKE.exe
let executeFAKEWithOutput workingDirectory script fsiargs =
  let (exitCode, exceptions) =
    Fake.DotNet.Fsi.exec (fun p ->
      { p with
          TargetProfile = Fake.DotNet.Fsi.Profile.Netcore
          WorkingDirectory = workingDirectory
          ToolPath = Fake.DotNet.Fsi.FsiTool.Internal 
      }) script fsiargs
  exitCode

// Documentation
let buildDocumentationTarget fsiargs target =
    Fake.Core.Trace.trace (sprintf "Building documentation (%s), this could take some time, please wait..." target)
    executeFAKEWithOutput "./docs/tools/" "./docs/tools/generate.fsx" fsiargs

Fake.Core.Target.create "GenerateDocs" (fun _ ->
     generateDocs ()
    //buildDocumentationTarget ["-D:RELEASE";"-d:REFERENCE"] "Default"
 )

// --------------------------------------------------------------------------------------
// Release Scripts

Fake.Core.Target.create "ReleaseDocs" (fun _ ->
    let tempDocsDir = "temp/gh-pages"
    Fake.IO.Shell.cleanDir tempDocsDir
    Fake.Tools.Git.Repository.cloneSingleBranch "" (gitHome + "/" + gitName + ".git") "gh-pages" tempDocsDir

    Fake.Tools.Git.Repository.fullclean tempDocsDir
    Fake.IO.Shell.copyRecursive "docs/output" tempDocsDir true |> Fake.Core.Trace.tracefn "%A"
    Fake.Tools.Git.Staging.stageAll tempDocsDir
    Fake.Tools.Git.Commit.exec tempDocsDir (sprintf "Update generated documentation for version %s" release.NugetVersion)
    Fake.Tools.Git.Branches.push tempDocsDir
)

Fake.Core.Target.create "Release" ignore

// --------------------------------------------------------------------------------------
// Run all targets by default. Invoke 'build <Target>' to override

Fake.Core.Target.create "All" ignore

"Clean"
  ==> "AssemblyInfo"
  ==> "Build"
  // ==> "RunTests"
  ==> "All"

"CleanDocs"
  ==> "GenerateDocs"
  ==> "ReleaseDocs"

"All" 
  ==> "CleanDocs"
  ==> "GenerateDocs"
  ==> "ReleaseDocs"
  ==> "Release"

Fake.Core.Target.runOrDefault "All"
