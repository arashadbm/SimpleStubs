﻿using Etg.SimpleStubs.CodeGen.Config;
using Etg.SimpleStubs.CodeGen.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Etg.SimpleStubs.CodeGen.CodeGen
{
    internal class ProjectStubber : IProjectStubber
    {
        private readonly IInterfaceStubber _interfaceStubber;
        private readonly SimpleStubsConfig _config;

        public ProjectStubber(IInterfaceStubber interfaceStubber, SimpleStubsConfig config)
        {
            _interfaceStubber = interfaceStubber;
            _config = config;
        }

        public async Task<StubProjectResult> StubProject(Project project, CompilationUnitSyntax cu)
        {
            var usings = new List<string>();
            foreach (Document document in project.Documents)
            {
                SyntaxTree syntaxTree = await document.GetSyntaxTreeAsync();
                SemanticModel semanticModel = await document.GetSemanticModelAsync();
                IEnumerable<InterfaceDeclarationSyntax> interfaces =
                    syntaxTree.GetRoot()
                        .DescendantNodes()
                        .OfType<InterfaceDeclarationSyntax>()
                        .Where(SatisfiesVisibilityConstraints);
                if (!interfaces.Any())
                {
                    continue;
                }

                foreach (var interfaceDclr in interfaces)
                {
                    try
                    {
                        INamedTypeSymbol interfaceType = semanticModel.GetDeclaredSymbol(interfaceDclr);
                        if (!_config.IgnoredInterfaces.Contains(interfaceType.GetQualifiedName()))
                        {
                            LogWarningsIfAny(semanticModel);
                            cu = _interfaceStubber.StubInterface(cu, interfaceDclr, semanticModel, _config);
                        }
                    }
                    catch (Exception e)
                    {
                        Trace.TraceError($"Could not generate stubs for interface {interfaceDclr}, Exception: {e}");
                    }
				}
				CopyUsings(syntaxTree, usings);
			}

            return new StubProjectResult(cu, usings);
        }

        private static void CopyUsings(SyntaxTree syntaxTree, List<string> usings)
        {
            foreach (UsingDirectiveSyntax usingDirectiveSyntax in syntaxTree.GetCompilationUnitRoot().Usings)
            {
                string usingName = usingDirectiveSyntax.Name.ToString();
                if (IsStaticUsing(usingDirectiveSyntax))
                {
                    usingName = $"static {usingName}";
                }
                usings.Add(usingName);
            }
        }

        private static bool IsStaticUsing(UsingDirectiveSyntax usingDirectiveSyntax)
        {
            return usingDirectiveSyntax.ChildTokens().Any(token => token.IsKind(SyntaxKind.StaticKeyword));
        }

        private bool SatisfiesVisibilityConstraints(InterfaceDeclarationSyntax i)
        {
            return i.IsPublic() || (_config.StubInternalInterfaces && i.IsInternal());
        }

        private void LogWarningsIfAny(SemanticModel semanticModel)
        {
            foreach (var diagnostic in semanticModel.GetDiagnostics())
            {
                Trace.TraceInformation(diagnostic.ToString());
            }
        }
    }
}