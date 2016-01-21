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

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;

namespace SonarLint.Descriptor
{
    public class RuleDetail
    {
        private const string CardinalitySingle = "SINGLE";

        public static RuleDetail Convert(RuleDescriptors.RuleDetail ruleDetail)
        {
            return new RuleDetail
            {
                Key = ruleDetail.Key,
                Title = ruleDetail.Title,
                Severity = ruleDetail.Severity.ToUpper(CultureInfo.InvariantCulture),
                Description = ruleDetail.Description,
                IsActivatedByDefault = ruleDetail.IsActivatedByDefault,
                Tags = ruleDetail.Tags,
                Parameters = ruleDetail.Parameters.Select(
                    parameter =>
                        new RuleParameter
                        {
                            Type = parameter.Type,
                            Key = parameter.Key,
                            Description = parameter.Description,
                            DefaultValue = parameter.DefaultValue
                        }).ToList(),
                Cardinality = CardinalitySingle
            };
        }

        public RuleDetail()
        {
            Tags = new List<string>();
            Parameters = new List<RuleParameter>();
        }

        [XmlElement("key")]
        public string Key { get; set; }
        [XmlElement("name")]
        public string Title { get; set; }
        [XmlElement("severity")]
        public string Severity { get; set; }
        [XmlElement("cardinality")]
        public string Cardinality { get; set; }

        [XmlIgnore]
        public string Description { get; set; }
        [XmlElement("description")]
        public XmlCDataSection DescriptionCDataSection
        {
            get
            {
                return new XmlDocument().CreateCDataSection(Description);
            }
            set
            {
                Description = value == null ? "" : value.Value;
            }
        }

        [XmlElement("tag")]
        public List<string> Tags { get; private set; }

        [XmlElement("param")]
        public List<RuleParameter> Parameters { get; private set; }

        [XmlIgnore]
        public bool IsActivatedByDefault { get; set; }
    }
}