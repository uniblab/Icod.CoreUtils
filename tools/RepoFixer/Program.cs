using System;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Xml.Linq;
using Microsoft.CodeAnalysis.Formatting;

class Program
{
    static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: RepoFixer <repo-root-path>");
            return 2;
        }

        var repoRoot = args[0];
        if (!Directory.Exists(repoRoot))
        {
            Console.Error.WriteLine($"Path not found: {repoRoot}");
            return 2;
        }

        // 1) Update csproj files
        foreach (var csproj in Directory.EnumerateFiles(repoRoot, "*.csproj", SearchOption.AllDirectories))
        {
            try
            {
                var xml = XDocument.Load(csproj);
                var ns = xml.Root?.Name.Namespace ?? XNamespace.None;

                var propertyGroups = xml.Root?.Elements(ns + "PropertyGroup").ToArray() ?? Array.Empty<XElement>();
                foreach (var pg in propertyGroups)
                {
                    var outputType = pg.Element(ns + "OutputType");
                    if (outputType is not null && outputType.Value.Trim().Equals("Exe", StringComparison.OrdinalIgnoreCase))
                    {
                        var outputPath = pg.Element(ns + "OutputPath");
                        if (outputPath is null)
                        {
                            pg.Add(new XElement(ns + "OutputPath", @"..\bin\$(Configuration)\"));
                        }
                        else
                        {
                            outputPath.Value = @"..\bin\$(Configuration)\";
                        }

                        // Determine assembly name from folder name (lowercase)
                        var folderName = new DirectoryInfo(Path.GetDirectoryName(csproj)!).Name;
                        var assemblyName = pg.Element(ns + "AssemblyName");
                        if (assemblyName is null)
                        {
                            pg.Add(new XElement(ns + "AssemblyName", folderName.ToLowerInvariant()));
                        }
                        else
                        {
                            assemblyName.Value = folderName.ToLowerInvariant();
                        }
                    }
                }

                xml.Save(csproj);
                Console.WriteLine($"Updated csproj: {csproj}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to update csproj {csproj}: {ex.Message}");
            }
        }

        // 2) Fix C# files: add braces to if statements and encode angle brackets in XML doc comments
        foreach (var cs in Directory.EnumerateFiles(repoRoot, "*.cs", SearchOption.AllDirectories))
        {
            try
            {
                var text = File.ReadAllText(cs);
                var tree = CSharpSyntaxTree.ParseText(text);
                var root = tree.GetRoot();

                var rewriter = new IfBracesAndXmlRewriter();
                var newRoot = rewriter.Visit(root);

                // Format and write file only if changed
                if (!newRoot.IsEquivalentTo(root))
                {
                    var workspace = new AdhocWorkspace();
                    var formatted = Formatter.Format(newRoot, workspace);
                    File.WriteAllText(cs, formatted.ToFullString());
                    Console.WriteLine($"Rewrote: {cs}");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to process {cs}: {ex.Message}");
            }
        }

        Console.WriteLine("RepoFixer: done.");
        return 0;
    }

    class IfBracesAndXmlRewriter : CSharpSyntaxRewriter
    {
        public override SyntaxNode? VisitIfStatement(IfStatementSyntax node)
        {
            // Ensure braces on the statement
            var newStatement = node.Statement;
            if (newStatement is not BlockSyntax)
            {
                newStatement = SyntaxFactory.Block(newStatement).WithTriviaFrom(node.Statement);
            }

            var newElse = node.Else;
            if (newElse is not null)
            {
                var elseStatement = newElse.Statement;
                if (elseStatement is not BlockSyntax)
                {
                    elseStatement = SyntaxFactory.Block(elseStatement).WithTriviaFrom(newElse.Statement);
                    newElse = newElse.WithStatement(elseStatement);
                }
            }

            var newNode = node.WithStatement(newStatement).WithElse(newElse);
            return base.VisitIfStatement(newNode);
        }

        public override SyntaxNode? VisitDocumentationCommentTrivia(DocumentationCommentTriviaSyntax node)
        {
            // Replace '<' and '>' in XML text tokens with entities.
            var newContent = node.Content.Select(c =>
            {
                if (c is XmlTextSyntax xt)
                {
                    var newXmlTextTokens = xt.TextTokens.Select(t =>
                    {
                        var txt = t.Text;
                        if (txt.Contains("<") || txt.Contains(">"))
                        {
                            txt = txt.Replace("<", "&lt;").Replace(">", "&gt;");
                            return SyntaxFactory.XmlTextLiteral(t.LeadingTrivia, txt, txt, t.TrailingTrivia);
                        }

                        return t;
                    });

                    return (XmlNodeSyntax)xt.WithTextTokens(SyntaxFactory.TokenList(newXmlTextTokens));
                }

                return c;
            });

            var newNode = node.WithContent(SyntaxFactory.List(newContent));
            return base.VisitDocumentationCommentTrivia(newNode);
        }
    }
}
