//------------------------------------------------------------------------------
// <copyright file="VSPackage1.cs" company="Company">
//     Copyright (c) Company.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.ComponentModelHost;
using SonarLint.Helpers;
using SonarLint.Rules.CSharp;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis;
using System.Linq;
using System.Timers;
using System.Reflection;
using CodeCracker.CSharp.Design;
using SonarLint.Rules.CSharp.DynamicAnalyzerLoader;

namespace MyVsix
{
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the
    /// IVsPackage interface and uses the registration attributes defined in the framework to
    /// register itself and its components with the shell. These attributes tell the pkgdef creation
    /// utility what data to put into .pkgdef file.
    /// </para>
    /// <para>
    /// To get loaded into VS, the package must be referred by &lt;Asset Type="Microsoft.VisualStudio.VsPackage" ...&gt; in .vsixmanifest file.
    /// </para>
    /// </remarks>
    [PackageRegistration(UseManagedResourcesOnly = true)]
    [Guid("e15e0ab5-ae20-42a1-8721-9abb18a5914b")]
    [ProvideAutoLoad("063BA845-A14B-40A1-97B7-33BFD00272E2")]
    [ProvideUIContextRule("063BA845-A14B-40A1-97B7-33BFD00272E2", "SonarLintIntegrationPackageActivation",
         "(HasCSProj | HasVBProj)",
        new string[] { "HasCSProj",
                       "HasVBProj" },
        new string[] { "SolutionHasProjectCapability:CSharp",
                       "SolutionHasProjectCapability:VB" }
    )]
    public sealed class VsPackage1 : Package
    {
        private object optionService;
        private VisualStudioWorkspace workspace;
        private Timer timer;
        private Option<bool> option;

        /// <summary>
        /// Initializes a new instance of the <see cref="VsPackage1"/> class.
        /// </summary>
        public VsPackage1()
        {
            // Inside this method you can place any initialization code that does not require
            // any Visual Studio service because at this point the package object is created but
            // not sited yet inside Visual Studio environment. The place to do all the other
            // initialization is the Initialize method.
        }

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
        protected override void Initialize()
        {
            base.Initialize();

            var componentModel = (IComponentModel)this.GetService(typeof(SComponentModel));
            this.workspace = componentModel.GetService<VisualStudioWorkspace>();
            this.optionService = GetOptionService();
            this.option = GetFullSolutionAnalysisOption();

            WrappingAnalyzer.AnalyzerPaths.Add(typeof (NameOfAnalyzer).Assembly.Location);

            timer = new Timer(10 * 1000);
            timer.Elapsed += Timer_Elapsed;
            timer.Start();
        }

        private object GetOptionService()
        {
            // reflection: get the IOptionService
            var types = typeof(PerLanguageOption<int>).Assembly.GetTypes();
            var interfaces = types
                .Where(t => t.IsInterface);

            var tOptionService = interfaces.First(t => t.Name == "IOptionService");

            return workspace.Services.GetType()
                            .GetMethod("GetService")
                            .MakeGenericMethod(tOptionService)
                            .Invoke(workspace.Services, null);
        }

        private static Option<bool> GetFullSolutionAnalysisOption()
        {
            // reflection: get the FullSolutionAnalysis option
            var path = Path.Combine(
                new FileInfo(typeof(Accessibility).Assembly.Location).Directory.FullName,
                @"Microsoft.CodeAnalysis.Features.dll"); //there's no public type in this DLL :-(

            return (Option<bool>)Assembly.LoadFile(path)
                .GetTypes()
                .Where(t => t.IsClass && t.IsSealed && t.IsAbstract)
                .First(t => t.Name == "RuntimeOptions")
                .GetField("FullSolutionAnalysis")
                .GetValue(null);
        }

        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            timer.Stop();

            var idsToToggle = new[]
            {
                "CC0108",  // nameof external (disabled by default)
                "CC0021",  // nameof
                "CC0001",  // use var
                "CC0105",  // use var external
                "CC0052"   // readonly field (compilation start action based)
            };

            var sonarAnalyzer = typeof(AsyncAwaitIdentifier);
            if (!SonarAnalysisContext.DisabledRules.Contains(sonarAnalyzer))
            {
                SonarAnalysisContext.DisabledRules.Add(sonarAnalyzer);
                WrappingAnalysisContext.DisabledDiagnosticIds.UnionWith(idsToToggle);
            }
            else
            {
                SonarAnalysisContext.DisabledRules.Remove(sonarAnalyzer);
                WrappingAnalysisContext.DisabledDiagnosticIds.ExceptWith(idsToToggle);
            }

            ReanalyzeSolution();

            timer.Start();
        }

        private void ReanalyzeSolution()
        {
            FlipFullSolutionAnalysisFlag();
            FlipFullSolutionAnalysisFlag();
        }

        private void FlipFullSolutionAnalysisFlag()
        {
            // reflection: flip analysis flag
            var options = (OptionSet)optionService.GetType().GetMethod("GetOptions").Invoke(optionService, null);
            var optionValue = options.GetOption(option);
            var newOptions = options.WithChangedOption(option, !optionValue);
            optionService.GetType().GetMethod("SetOptions").Invoke(optionService, new object[] { newOptions });
        }
    }
}
