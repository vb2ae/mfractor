﻿using System;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MFractor.Analytics;
using MFractor.CSharp;
using MFractor.Editor.Tooltips;
using MFractor.Workspace;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;

namespace MFractor.Editor.CSharp.Tooltips
{
    [PartCreationPolicy(CreationPolicy.NonShared)]
    [Export]
    class DateTimeFormatPreviewTooltipsQuickInfoSource : IAsyncQuickInfoSource
    {
        readonly Logging.ILogger log = Logging.Logger.Create();

        readonly IAnalyticsService analyticsService;
        readonly IWorkspaceService workspaceService;
        readonly IDateTimeFormatValidator dateTimeFormatValidator;

        string filePath;
        ProjectId projectId;

        [ImportingConstructor]
        public DateTimeFormatPreviewTooltipsQuickInfoSource(IAnalyticsService analyticsService,
                                            IWorkspaceService workspaceService,
                                            IDateTimeFormatValidator dateTimeFormatValidator)
        {
            this.analyticsService = analyticsService;
            this.workspaceService = workspaceService;
            this.dateTimeFormatValidator = dateTimeFormatValidator;
        }

        public void Initialise(string filePath,
                               ProjectId projectId)
        {
            this.filePath = filePath;
            this.projectId = projectId;
        }

        public SyntaxTree SyntaxTreeForDocument(Document analysisDocument)
        {
            if (analysisDocument == null)
            {
                return null;
            }

            if (!analysisDocument.TryGetSyntaxTree(out var syntaxTree))
            {
                return null;
            }

            return syntaxTree;
        }

        // This is called on a background thread.
        public Task<QuickInfoItem> GetQuickInfoItemAsync(IAsyncQuickInfoSession session,
                                                         CancellationToken cancellationToken)
        {
            try
            {
                var project = workspaceService.CurrentWorkspace.CurrentSolution.GetProject(projectId);
                if (project == null)
                {
                    return Task.FromResult<QuickInfoItem>(null);
                }

                var analysisDocument = project.Documents.FirstOrDefault(d => d.FilePath == filePath);
                if (analysisDocument is null)
                {
                    return Task.FromResult<QuickInfoItem>(null);
                }

                var syntaxTree = SyntaxTreeForDocument(analysisDocument);
                if (syntaxTree is null)
                { 
                    return Task.FromResult<QuickInfoItem>(null);
                }

                var textBuffer = session.TextView.TextBuffer;
                var triggerPoint = session.GetTriggerPoint(textBuffer);

                var syntax = GetSyntaxAtLocation(syntaxTree, triggerPoint.GetPosition(textBuffer.CurrentSnapshot)) as LiteralExpressionSyntax;

                var span = GetStringSpan(syntax);

                if (span == null)
                {
                    return Task.FromResult<QuickInfoItem>(null);
                }

                var format = syntax.Token.ValueText;

                if (!dateTimeFormatValidator.IsValidDateTimeFormat(format))
                {
                    return Task.FromResult<QuickInfoItem>(null);
                }

                var content = "DateTime Format Example:" + Environment.NewLine + DateTime.Now.ToString(format);

                var textRun = new ClassifiedTextRun(PredefinedClassificationTypeNames.Other, content);

                var classifiedText = new Microsoft.VisualStudio.Text.Adornments.ClassifiedTextElement(textRun);

                var trackingSpan = textBuffer.CurrentSnapshot.CreateTrackingSpan(Span.FromBounds(span.Value.Start, span.Value.End), SpanTrackingMode.EdgeInclusive);

                analyticsService.Track("C# Date Time Format Preview Tooltip");

                return Task.FromResult(new QuickInfoItem(trackingSpan, classifiedText));
            }
            catch (Exception ex)
            {
                log?.Exception(ex);
            }

            return Task.FromResult<QuickInfoItem>(null);
        }

        public LiteralExpressionSyntax GetSyntaxAtLocation(SyntaxTree syntaxTree, int offset)
        {
            var syntaxRoot = syntaxTree.GetRoot();

            var span = TextSpan.FromBounds(offset, offset + 1);

            SyntaxNode node = null;
            try
            {
                if (syntaxRoot.Span.IntersectsWith(span) && span.Start >= syntaxRoot.SpanStart && span.End <= syntaxRoot.Span.End)
                {
                    node = syntaxRoot.FindNode(span);
                }
            }
            catch (ArgumentOutOfRangeException aex)
            {
                log?.Info(aex.ToString());
            }
            catch (Exception ex)
            {
                log?.Warning(ex.ToString());
            }

            if (node is ArgumentSyntax argumentSyntax
                && argumentSyntax.Expression is LiteralExpressionSyntax inner)
            {
                node = inner;
            }

            return node as LiteralExpressionSyntax;
        }


        TextSpan? GetStringSpan(SyntaxNode syntax)
        {
            if (syntax is LiteralExpressionSyntax literalExpressionSyntax)
            {
                if (literalExpressionSyntax.Token.IsKind(SyntaxKind.StringLiteralExpression)
                    || literalExpressionSyntax.Token.IsKind(SyntaxKind.StringLiteralToken))
                {
                    var span = syntax.Span;

                    return TextSpan.FromBounds(span.Start + 1, span.End - 1);
                }
            }

            return null;
        }

        public void Dispose()
        {
            // This provider does not perform any cleanup.
        }
    }
}