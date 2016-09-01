using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RegressionCheckConverter
{
    class Program
    {
        static void Main(string[] args)
        {
            var legacyFilePath = args[0];
            var newExpectedFolder = args[1];

            var legacyId = args[2];
            var newId = args[3];

            var legacyIssues = ReadLegacyFile(legacyFilePath, legacyId)
                .OrderBy(i => i.Path)
                .ThenBy(i => i.Line)
                .ThenBy(i => i.Message);

            WriteIssues(legacyIssues, newId, "Legacy");

            var newIssues = ReadNewExpectedFolder(newExpectedFolder, newId)
                .OrderBy(i => i.Path)
                .ThenBy(i => i.Line)
                .ThenBy(i => i.Message);

            WriteIssues(newIssues, newId, "New");
        }

        private static IEnumerable<Issue> ReadNewExpectedFolder(string newExpectedFolder, string newId)
        {
            var files = Directory.GetFiles(newExpectedFolder, "*-" + newId + ".json");
            foreach (var file in files)
            {
                var expected = JsonConvert.DeserializeObject<NewExpectedFile>(File.ReadAllText(file));
                foreach (var issue in expected.Issues.Where(i=> i.Location.Uri.EndsWith(".vb")))
                {
                    yield return new Issue
                    {
                        Line = issue.Location?.Region?.StartLine ?? -1,
                        Message = issue.Message,
                        Path = issue.Location.Uri
                    };
                }
            }
        }

        private static IEnumerable<Issue> ReadLegacyFile(string path, string legacyId)
        {
            using (var file = new StreamReader(path))
            {
                string line;
                while ((line = file.ReadLine()) != null)
                {
                    var parts = line.Split('\t');
                    if (parts[0] != legacyId)
                    {
                        continue;
                    }

                    if (parts[1].EndsWith("designer.vb", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    int lineNumber;
                    if (!int.TryParse(parts[2], out lineNumber))
                    {
                        lineNumber = -1;
                    }

                    yield return new Issue
                    {
                        Path = parts[1].Replace("C:/dev/sonartv/", "").Replace(" ", "%20").Replace("/", @"\"),
                        Line = lineNumber,
                        Message = parts[3]
                    };
                }
            }
        }

        private static void WriteIssues(IEnumerable<Issue> issues, string newId, string folder)
        {
            Directory.CreateDirectory(folder);
            using (var file = new StreamWriter(folder + "\\" + newId + ".txt"))
            {
                foreach (var issue in issues)
                {
                    file.WriteLine(issue);
                }
            }
        }

        class Issue
        {
            public string Path { get; set; }
            public string Message { get; set; }
            public int Line { get; set; }

            public override string ToString()
            {
                return Path + "\t" + Line /*+ "\t" + Message*/;
            }
        }


        public class NewExpectedFile
        {
            [JsonProperty(PropertyName = "issues")]
            public List<NewIssue> Issues { get; set; }
        }

        public class NewIssue
        {
            [JsonProperty(PropertyName = "id")]
            public string Id { get; set; }
            [JsonProperty(PropertyName = "message")]
            public string Message { get; set; }
            [JsonProperty(PropertyName = "location")]
            public Location Location { get; set; }
        }

        public class Location
        {
            [JsonProperty(PropertyName = "uri")]
            public string Uri { get; set; }
            [JsonProperty(PropertyName = "region")]
            public Region Region { get; set; }
        }

        public class Region
        {
            [JsonProperty(PropertyName = "startLine")]
            public int StartLine { get; set; }
        }
    }
}
