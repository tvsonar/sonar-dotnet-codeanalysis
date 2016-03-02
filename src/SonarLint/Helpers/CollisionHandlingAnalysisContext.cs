/*
 * SonarLint for Visual Studio
 * Copyright (C) 2015-2016 SonarSource SA
 * mailto:contact@sonarsource.com
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public
 * License along with this program; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02
 */


using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Reflection;

namespace SonarLint.Helpers
{
    public class CollisionHandlingAnalysisContext : SonarAnalysisContext
    {
        private static readonly AssemblyName AnalyzerAssemblyName =
            new AssemblyName(typeof(CollisionHandlingAnalysisContext).GetTypeInfo().Assembly.FullName);
        private static readonly Version AnalyzerVersion = AnalyzerAssemblyName.Version;
        private static readonly string AnalyzerName = AnalyzerAssemblyName.Name;
        private static Workspace workspace;
        private static bool triedQueryingWorkspace;

        public CollisionHandlingAnalysisContext(AnalysisContext context) : base(context)
        {
            //this is important here: we force the initialization of the workspace, which requires loading the Workspace type
            workspace = null;
            triedQueryingWorkspace = false;
        }

        protected override bool IsAnalysisDisabled(SyntaxTree tree)
        {
            if (tree == null)
            {
                return false;
            }

            if (!triedQueryingWorkspace &&
                workspace == null &&
                !Workspace.TryGetWorkspace(tree.GetText().Container, out workspace))
            {
                // we don't set the triedQueryingWorkspace here, because
                // there are empty trees in the Workspace, where TryGetWorkspace returns false
                return false;
            }

            triedQueryingWorkspace = true;

            var references = workspace?.CurrentSolution?.GetDocument(tree)?.Project?.AnalyzerReferences;
            if (references != null)
            {
                foreach (var reference in references.Where(a => a.Display == AnalyzerName))
                {
                    var version = (reference.Id as AssemblyIdentity)?.Version;
                    return version != AnalyzerVersion;
                }
            }

            return false;
        }
    }
}
