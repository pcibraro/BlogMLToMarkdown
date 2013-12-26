using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace BlogMLToMarkdown
{
    class Program
    {
        private const string postFormat = @"---
layout: post
title: ""{0}""
date: {1}
comments: true
categories: {2}
---

{3}
";
        private const string blogMLNamespace = "http://www.blogml.com/2006/09/BlogML";

        static void Main(string[] args)
        {
            string documentContent = null;
            using(var sr = new StreamReader(args[0]))
            {
                documentContent = sr.ReadToEnd();
            }

            var document = XDocument.Load(XmlReader.Create(new StringReader(documentContent), new XmlReaderSettings
                {
                    IgnoreComments = true,
                    CheckCharacters = false,
                }));

            var allCategories = document.Root.Elements(XName.Get("categories", blogMLNamespace))
                .Elements()
                .ToArray();

            var posts = document.Root.Elements(XName.Get("posts", blogMLNamespace))
                .Elements()
                .ToArray();

            if (!Directory.Exists("output"))
            {
                Directory.CreateDirectory("output");
            }

            foreach (var post in posts)
            {
                var dateCreated = DateTime.Parse(post.Attribute("date-created").Value).ToString("yyyy-MM-dd");
                var title = post.Descendants(XName.Get("title", blogMLNamespace)).First().Value;
                var content = post.Descendants(XName.Get("content", blogMLNamespace)).First().Value;
                var url = post.Attribute("post-url").Value;
                var postname = url.Substring(url.LastIndexOf("/") + 1).Replace(".aspx", "");
               
                var categories = post.Descendants(XName.Get("category", blogMLNamespace))
                    .Select(c1 => c1.Attribute("ref").Value)
                    .ToArray();

                var firstCategory = allCategories.Where(c1 => categories.Any(c2 => c2 == c1.Attribute(XName.Get("id")).Value))
                    .Select(c1 => c1.Elements().First().Value)
                    .ToArray()
                    .FirstOrDefault();
                
                if(firstCategory != null)
                    firstCategory = firstCategory.Replace(" ", "-");

                var markdown = FormatCode(ConvertHtmlToMarkdown(content));

                var blog = string.Format(postFormat, title, dateCreated, string.Join(", ", firstCategory), markdown);
                
                using (var sw = File.CreateText ("output\\" + dateCreated + "-" + postname + ".markdown"))
                {
                    sw.Write(blog);
                };
                
            }
        }

        static string ConvertHtmlToMarkdown(string source)
        {
            string args = String.Format(@"-r html -t markdown");

            var startInfo = new ProcessStartInfo("pandoc.exe", args)
            {
                RedirectStandardOutput = true,
                RedirectStandardInput = true,
                UseShellExecute = false
            };

            var process = new Process { StartInfo = startInfo };
            process.Start();

            var inputBuffer = Encoding.UTF8.GetBytes(source);
            process.StandardInput.BaseStream.Write(inputBuffer, 0, inputBuffer.Length);
            process.StandardInput.Close();

            process.WaitForExit(2000);
            using (var sr = new StreamReader(process.StandardOutput.BaseStream))
            {
                return sr.ReadToEnd();
            }
        }

        static readonly Regex _codeRegex = new Regex(@"~~~~ \{\.csharpcode\}(?<code>.*?)~~~~", RegexOptions.Compiled | RegexOptions.Singleline);

        static string FormatCode(string content)
        {
            return _codeRegex.Replace(content, match =>
            {
                var code = match.Groups["code"].Value;
                return "```" + GetLanguage(code) + code + "```";
            });
        }

        static string GetLanguage(string code)
        {
            var trimmedCode = code.Trim();
            if (trimmedCode.Contains("<%= ") || trimmedCode.Contains("<%: ")) return "aspx-cs";
            if (trimmedCode.StartsWith("<script") || trimmedCode.StartsWith("<table")) return "html";
            return "csharp";
        }
    }
}
