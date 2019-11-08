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
nuget Fake.Core.ReleaseNotes //"
#load "./.fake/build.fsx/intellisense.fsx"
open Fake.IO.Globbing.Operators
open Fake.Core.TargetOperators
open Fake.DotNet

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
    |> ignore

Fake.Core.Target.create "GenerateDocs" (fun _ ->
    buildDocumentationTarget ["-D:RELEASE";"-d:REFERENCE"] "Default"
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
