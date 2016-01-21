﻿/*
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
using System.Globalization;
using SonarLint.Common;
using System.Collections.Immutable;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Reflection;
using System.IO;
using System.Collections.Concurrent;

namespace SonarLint.Helpers
{
    public static class ParameterLoader
    {
        public static readonly string ParameterConfigurationFileName = "SonarLint.xml";

        public class RuleParameterValue
        {
            public string ParameterKey { get; set; }
            public string ParameterValue { get; set; }
        }
        public class RuleParameterValues
        {
            public string RuleId { get; set; }
            public List<RuleParameterValue> ParameterValues { get; set; } = new List<RuleParameterValue>();
        }

        //todo: this can become private when we remove the template rule
        public static ImmutableList<RuleParameterValues> ParseParameters(XContainer xml)
        {
            var builder = ImmutableList.CreateBuilder<RuleParameterValues>();
            foreach (var rule in xml.Descendants("Rule").Where(e => e.Elements("Parameters").Any()))
            {
                var analyzerId = rule.Elements("Key").Single().Value;

                var parameterValues = rule
                    .Elements("Parameters").Single()
                    .Elements("Parameter")
                    .Select(e => new RuleParameterValue
                    {
                        ParameterKey = e.Elements("Key").Single().Value,
                        ParameterValue = e.Elements("Value").Single().Value
                    });

                var pvs = new RuleParameterValues
                {
                    RuleId = analyzerId
                };
                pvs.ParameterValues.AddRange(parameterValues);

                builder.Add(pvs);
            }

            return builder.ToImmutable();
        }

        private readonly static ConcurrentDictionary<DiagnosticAnalyzer, byte> ProcessedAnalyzers = new ConcurrentDictionary<DiagnosticAnalyzer, byte>();

        public static void SetParameterValues(DiagnosticAnalyzer parameteredAnalyzer,
            AnalyzerOptions options)
        {
            if (ProcessedAnalyzers.ContainsKey(parameteredAnalyzer))
            {
                return;
            }

            var additionalFile = options.AdditionalFiles
                .FirstOrDefault(f => ConfigurationFilePathMatchesExpected(f.Path));

            if (additionalFile == null)
            {
                return;
            }

            var filePath = additionalFile.Path;
            var xml = XDocument.Load(filePath);
            var parameters = ParseParameters(xml);

            var propertyParameterPairs = parameteredAnalyzer.GetType()
                .GetProperties()
                .Select(p => new { Property = p, Descriptor = p.GetCustomAttributes<RuleParameterAttribute>().SingleOrDefault() })
                .Where(p => p.Descriptor != null);

            foreach (var propertyParameterPair in propertyParameterPairs)
            {
                var parameter = parameters
                    .FirstOrDefault(p => p.RuleId == parameteredAnalyzer.SupportedDiagnostics.Single().Id);

                if (parameter == null)
                {
                    return;
                }

                var parameterValue = parameter.ParameterValues
                    .FirstOrDefault(pv => pv.ParameterKey == propertyParameterPair.Descriptor.Key);

                if (parameterValue == null)
                {
                    return;
                }

                var value = parameterValue.ParameterValue;
                var convertedValue = ChangeParameterType(value, propertyParameterPair.Descriptor.Type);
                propertyParameterPair.Property.SetValue(parameteredAnalyzer, convertedValue);
            }

            ProcessedAnalyzers.AddOrUpdate(parameteredAnalyzer, 0, (a, b) => b);
        }

        private static object ChangeParameterType(string parameter, PropertyType type)
        {
            switch (type)
            {
                case PropertyType.String:
                    return parameter;
                case PropertyType.Integer:
                    return int.Parse(parameter, NumberStyles.None, CultureInfo.InvariantCulture);
                default:
                    throw new NotSupportedException();
            }
        }

        public static bool ConfigurationFilePathMatchesExpected(string path)
        {
            return new FileInfo(path).Name.Equals(ParameterConfigurationFileName, StringComparison.InvariantCultureIgnoreCase);
        }
    }
}
