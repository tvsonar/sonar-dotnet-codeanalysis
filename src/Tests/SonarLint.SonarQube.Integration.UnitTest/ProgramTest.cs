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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarLint.Common;
using SonarLint.Helpers;
using SonarLint.Runner;
using System.IO;
using System.Linq;
using SonarAnalyzer.Protobuf;
using Google.Protobuf;
using System.Collections.Generic;

namespace SonarLint.UnitTest
{
    [TestClass]
    public class ProgramTest
    {
        private const string OutputFolderName = "Output";
        private const string TestInputFolder = "TestResources";
        private static readonly string ExpectedFilePath = $@"{TestInputFolder}\TestInput.cs";

        [TestMethod]
        public void End_To_End_CSharp()
        {
            Program.Main(new [] {
                $@"{TestInputFolder}\{ParameterLoader.ParameterConfigurationFileName}",
                OutputFolderName,
                AnalyzerLanguage.CSharp.ToString()});

            var textActual = new string(File.ReadAllText(Path.Combine(OutputFolderName, Program.AnalysisOutputFileName))
                .ToCharArray()
                .Where(c => !char.IsWhiteSpace(c))
                .ToArray());

            CheckExpected(textActual);
            CheckNotExpected(textActual);

            var testFileContent = File.ReadAllLines(Path.Combine(TestInputFolder, "TestInput.cs"));

            CheckTokenInfoFile(testFileContent);
            CheckTokenReferenceFile(testFileContent);
        }

        private static void CheckTokenReferenceFile(string[] testInputFileLines)
        {
            var refInfos = new List<FileTokenReferenceInfo>();

            using (var input = File.OpenRead(Path.Combine(OutputFolderName, Program.TokenReferenceInfosFileName)))
            {
                while (input.Position != input.Length)
                {
                    var ri = new FileTokenReferenceInfo();
                    ri.MergeDelimitedFrom(input);
                    refInfos.Add(ri);
                }
            }

            Assert.AreEqual(1, refInfos.Count);
            var refInfo = refInfos.First();
            Assert.AreEqual(ExpectedFilePath, refInfo.FilePath);
            Assert.AreEqual(3, refInfo.Reference.Count);

            var declarationPosition = refInfo.Reference[2].Declaration;
            Assert.AreEqual(declarationPosition.StartLine, declarationPosition.EndLine);
            var tokenText = testInputFileLines[declarationPosition.StartLine - 1].Substring(
                declarationPosition.StartOffset,
                declarationPosition.EndOffset - declarationPosition.StartOffset);
            Assert.AreEqual("x", tokenText);

            Assert.AreEqual(1, refInfo.Reference[2].Reference.Count);
            var referencePosition = refInfo.Reference[2].Reference[0];
            Assert.AreEqual(referencePosition.StartLine, referencePosition.EndLine);
            tokenText = testInputFileLines[referencePosition.StartLine - 1].Substring(
                referencePosition.StartOffset,
                referencePosition.EndOffset - referencePosition.StartOffset);
            Assert.AreEqual("x", tokenText);
        }

        private static void CheckTokenInfoFile(string[] testInputFileLines)
        {
            var tokenInfos = new List<FileTokenInfo>();

            using (var input = File.OpenRead(Path.Combine(OutputFolderName, Program.TokenInfosFileName)))
            {
                while (input.Position != input.Length)
                {
                    var tokenInfo = new FileTokenInfo();
                    tokenInfo.MergeDelimitedFrom(input);
                    tokenInfos.Add(tokenInfo);
                }
            }

            Assert.AreEqual(1, tokenInfos.Count);
            var token = tokenInfos.First();
            Assert.AreEqual(ExpectedFilePath, token.FilePath);
            Assert.AreEqual(34, token.TokenInfo.Count);
            Assert.AreEqual(TokenType.DeclarationName, token.TokenInfo[2].TokenType);

            var tokenPosition = token.TokenInfo[2].TextRange;
            Assert.AreEqual(tokenPosition.StartLine, tokenPosition.EndLine);
            var tokenText = testInputFileLines[tokenPosition.StartLine - 1].Substring(
                tokenPosition.StartOffset,
                tokenPosition.EndOffset - tokenPosition.StartOffset);
            Assert.AreEqual("TTTestClass", tokenText);
        }

        private static void CheckExpected(string textActual)
        {
            var expectedContent = new[]
            {
                $@"<AnalysisOutput><Files><File><Path>{ExpectedFilePath}</Path>",
                @"<Metrics><Lines>17</Lines>",
                @"<Issue><Id>S1134</Id><Line>3</Line>",
                @"<Issue><Id>S1135</Id><Line>5</Line>",
                @"<Id>S101</Id><Line>1</Line><Message>Renameclass""TTTestClass""tomatchcamelcasenamingrules,considerusing""TtTestClass"".</Message>",
                @"<Id>S103</Id><Line>11</Line><Message>Splitthis21characterslongline(whichisgreaterthan10authorized).</Message>",
                @"<Id>S103</Id><Line>14</Line><Message>Splitthis17characterslongline(whichisgreaterthan10authorized).</Message>",
                @"<Id>S104</Id><Line>1</Line><Message>Thisfilehas17lines,whichisgreaterthan10authorized.Splititintosmallerfiles.</Message>"
            };

            foreach (var expected in expectedContent)
            {
                if (!textActual.Contains(expected))
                {
                    Assert.Fail("Generated output file doesn't contain expected string '{0}'", expected);
                }
            }
        }

        private static void CheckNotExpected(string textActual)
        {
            var notExpectedContent = new[]
            {
                @"<Id>S1116</Id><Line>14</Line>"
            };

            foreach (var notExpected in notExpectedContent)
            {
                if (textActual.Contains(notExpected))
                {
                    Assert.Fail("Generated output file contains not expected string '{0}'", notExpected);
                }
            }
        }
    }
}
