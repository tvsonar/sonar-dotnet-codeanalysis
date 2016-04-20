param($installPath, $toolsPath, $package, $project)

if ($project.DTE.Version -ne '14.0')
{
    throw 'This package can only be installed on Visual Studio 2015'
}

if ($project.Object.AnalyzerReferences -eq $null)
{
    throw 'This package cannot be installed without an analyzer reference (C# and VB.NET only)'
}

Write-Host "----------------------------------------------------------------------------------------"
Write-Host " SonarLint is deprecated, removing it from the project and adding SonarAnalyzer instead "
Write-Host "----------------------------------------------------------------------------------------"

# $project.Type gives the language name like (C# or VB.NET)
if($project.Type -eq "C#")
{
    Install-Package SonarAnalyzer.CSharp
}
if($project.Type -eq "VB.NET")
{
    Install-Package SonarAnalyzer.VisualBasic
}

Uninstall-Package SonarLint

