﻿/*
 * SonarAnalyzer for .NET
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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarAnalyzer.Rules;
using SonarAnalyzer.Rules.CSharp;

namespace SonarAnalyzer.UnitTest.Rules
{
    [TestClass]
    public class UseShortCircuitingOperatorsTest
    {
        [TestMethod, TestCategory("Rule")]
        public void UseShortCircuitingOperators()
        {
            Verifier.VerifyAnalyzer(@"TestCases\UseShortCircuitingOperators.vb", new UseShortCircuitingOperators());
        }

        [TestMethod, TestCategory("CodeFix")]
        public void UseShortCircuitingOperators_CodeFix()
        {
            Verifier.VerifyCodeFix(
                @"TestCases\UseShortCircuitingOperators.vb",
                @"TestCases\UseShortCircuitingOperators.Fixed.vb",
                new UseShortCircuitingOperators(),
                new UseShortCircuitingOperatorsFixProvider());
        }
    }
}
