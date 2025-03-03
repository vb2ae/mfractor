﻿using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using MFractor.Progress;
using MFractor.Work;
using MFractor.Work.WorkUnits;
using Microsoft.VisualStudio.LanguageServices;
using MFractor;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis;
using MFractor.Utilities;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MFractor.Workspace;
using MFractor.Ide.WorkUnits;
using MFractor.Ide;

namespace MFractor.VS.Windows.WorkUnitHandlers
{
    class NavigateToSymbolWorkUnitHandler : WorkUnitHandler<NavigateToSymbolWorkUnit>
    {
        readonly Lazy<IDispatcher> dispatcher;
        public IDispatcher Dispatcher => dispatcher.Value;

        readonly Lazy<IWorkspaceService> workspaceService;
        IWorkspaceService WorkspaceService => workspaceService.Value;

        readonly Lazy<IActiveDocument> activeDocument;
        IActiveDocument ActiveDocument => activeDocument.Value;

        readonly Logging.ILogger log = Logging.Logger.Create();

        [ImportingConstructor]
        public NavigateToSymbolWorkUnitHandler(Lazy<IWorkspaceService> workspaceService,
                                               Lazy<IDispatcher> dispatcher,
                                               Lazy<IActiveDocument> activeDocument)
        {
            this.workspaceService = workspaceService;
            this.dispatcher = dispatcher;
            this.activeDocument = activeDocument;
        }
         public override async Task<IWorkExecutionResult> OnExecute(NavigateToSymbolWorkUnit workUnit, IProgressMonitor progressMonitor)
        {
            if (TryGetNavigationTarget(workUnit.Symbol, out var filePath, out var span))
            {
                var result = new WorkExecutionResult();
                result.AddPostProcessedWorkUnit(new NavigateToFileSpanWorkUnit(span, filePath));

                return result;
            }

            await Dispatcher.InvokeOnMainThreadAsync(() =>
            {
                try
                {
                    if (WorkspaceService.CurrentWorkspace is VisualStudioWorkspace vsWorkspace)
                    {
                        vsWorkspace.TryGoToDefinition(workUnit.Symbol, ActiveDocument.CompilationProject, CancellationToken.None);
                    }
                }
                catch (Exception ex)
                {
                    log?.Exception(ex);
                }
            });

             return default;
        }

        bool TryGetNavigationTarget(ISymbol symbol, out string filePath, out TextSpan span)
        {
            filePath = string.Empty;
            span = TextSpan.FromBounds(0, 0);

            if (symbol is INamedTypeSymbol namedTypeSymbol)
            {
                var classDeclarationSyntax = namedTypeSymbol.GetNonAutogeneratedSyntax() as ClassDeclarationSyntax;
                if (classDeclarationSyntax != null)
                {
                    filePath = classDeclarationSyntax.SyntaxTree.FilePath;
                    span = classDeclarationSyntax.Identifier.Span;
                    return true;
                }
            }
            else if (SymbolHelper.TryGetMemberLocation(symbol, out filePath, out span))
            {
                return true;
            }

            return false;
        }
    }
}
