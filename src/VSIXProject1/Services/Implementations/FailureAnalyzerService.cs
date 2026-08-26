using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ContinueVS.Core.Enums;
using ContinueVS.Core.Types;
using ContinueVS.Services.Interfaces;
using Newtonsoft.Json.Linq;

namespace ContinueVS.Services.Implementations
{
    internal class FailureAnalyzerService : IFailureAnalyzerService
    {
        private readonly ILlmService _llmService;
        private readonly IBridgeLogger _logger;

        public FailureAnalyzerService(ILlmService llmService, IBridgeLogger logger)
        {
            if (llmService == null) throw new ArgumentNullException(nameof(llmService));
            if (logger == null) throw new ArgumentNullException(nameof(logger));
            _llmService = llmService;
            _logger = logger;
        }

        public async Task<RefinementAttempt> AnalyzeFailureAsync(string errorOutput, CodeChange previousChange, string sessionContext, bool isAutonomousMode, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(errorOutput))
                throw new ArgumentException("errorOutput must not be empty.", nameof(errorOutput));
            if (previousChange == null) throw new ArgumentNullException(nameof(previousChange));

            try
            {
                var errorAnalysis = ParseErrorOutput(errorOutput);
                await _logger.WriteInfoAsync($"[gap29_8_6] Error parsed - Type: {errorAnalysis.ErrorType}, Message: {errorAnalysis.Message}");

                var hypotheses = await GenerateHypothesesAsync(errorAnalysis, previousChange, sessionContext, isAutonomousMode, cancellationToken);
                double confidence = CalculateConfidenceScore(hypotheses, errorAnalysis.ErrorType);

                CodeChange? refinedChange = null;
                string? approachDescription = null;

                if (hypotheses.Any() && confidence >= 0.3)
                {
                    refinedChange = GenerateRefinedChange(previousChange, hypotheses, errorAnalysis);
                    approachDescription = FormatApproachDescription(hypotheses, confidence);
                }

                var attempt = new RefinementAttempt(errorAnalysis, 1)
                {
                    Hypotheses = hypotheses,
                    RefinedChange = refinedChange,
                    ConfidenceScore = confidence,
                    ApproachDescription = approachDescription
                };

                await _logger.WriteInfoAsync($"[gap29_8_6] Refinement generated - Confidence: {confidence:F2}, Viable: {attempt.IsViable()}");
                return attempt;
            }
            catch (OperationCanceledException)
            {
                await _logger.WriteInfoAsync("[gap29_8_6] Failure analysis cancelled.");
                throw;
            }
            catch (Exception ex)
            {
                await _logger.WriteErrorAsync($"[gap29_8_6] Failure analysis error: {ex.Message}", ex);
                throw;
            }
        }

        private ErrorAnalysisResult ParseErrorOutput(string errorOutput)
        {
            if (string.IsNullOrWhiteSpace(errorOutput))
                return new ErrorAnalysisResult(ErrorType.Unknown, "Empty error output.", "Unknown");

            if (IsCompilationError(errorOutput))
                return ParseCompilationError(errorOutput);
            if (IsTestFailure(errorOutput))
                return ParseTestFailure(errorOutput);
            if (IsException(errorOutput))
                return ParseException(errorOutput);

            return new ErrorAnalysisResult(ErrorType.Unknown, errorOutput.Substring(0, Math.Min(200, errorOutput.Length)), "Unknown") { RawOutput = errorOutput };
        }

        private static bool IsCompilationError(string output) => Regex.IsMatch(output, @"(error CS\d+|compilation error|build failed)", RegexOptions.IgnoreCase);
        private static bool IsTestFailure(string output) => Regex.IsMatch(output, @"(Assert\.|test failed|FAILED|xunit|nunit|mstest)", RegexOptions.IgnoreCase);
        private static bool IsException(string output) => Regex.IsMatch(output, @"(Exception|Error|at\s+[A-Za-z])", RegexOptions.IgnoreCase);

        private ErrorAnalysisResult ParseCompilationError(string output)
        {
            var match = Regex.Match(output, @"(?<file>[^\s]+\.cs)\((?<line>\d+),\s*(?<col>\d+)\):\s*error\s*(?<code>CS\d+):\s*(?<msg>[^\n]+)");
            if (match.Success)
            {
                return new ErrorAnalysisResult(ErrorType.Compilation, match.Groups["msg"].Value, match.Groups["code"].Value)
                {
                    FilePath = match.Groups["file"].Value,
                    LineNumber = int.Parse(match.Groups["line"].Value, CultureInfo.InvariantCulture),
                    RawOutput = output
                };
            }

            var errorMatch = Regex.Match(output, @"error[:\s]*(.+?)(?:\n|$)");
            return new ErrorAnalysisResult(ErrorType.Compilation, errorMatch.Success ? errorMatch.Groups[1].Value : "Compilation error", "Compilation") { RawOutput = output };
        }

        private ErrorAnalysisResult ParseTestFailure(string output)
        {
            var assertMatch = Regex.Match(output, @"Assert\.(?<method>\w+)\((?<expected>[^,]+),\s*(?<actual>[^)]+)\)");
            var failMatch = Regex.Match(output, @"(?<msg>Expected:.*?Actual:.+?)(?:\n|$)", RegexOptions.Singleline);

            string message = failMatch.Success ? failMatch.Groups["msg"].Value : (assertMatch.Success ? "Assertion failed" : "Test failure");
            string category = assertMatch.Success ? assertMatch.Groups["method"].Value : "AssertionFailure";

            return new ErrorAnalysisResult(ErrorType.TestFailure, message, category) { RawOutput = output };
        }

        private ErrorAnalysisResult ParseException(string output)
        {
            var exMatch = Regex.Match(output, @"(?<type>\w+Exception):\s*(?<msg>[^\n]+)");
            if (exMatch.Success)
            {
                return new ErrorAnalysisResult(ErrorType.Exception, exMatch.Groups["msg"].Value, exMatch.Groups["type"].Value) 
                { 
                    StackTrace = output, 
                    RawOutput = output 
                };
            }

            return new ErrorAnalysisResult(ErrorType.Exception, output.Substring(0, Math.Min(200, output.Length)), "Exception") { StackTrace = output, RawOutput = output };
        }

        private async Task<List<string>> GenerateHypothesesAsync(ErrorAnalysisResult errorAnalysis, CodeChange previousChange, string sessionContext, bool isAutonomousMode, CancellationToken cancellationToken)
        {
            var prompt = BuildHypothesisPrompt(errorAnalysis, previousChange, sessionContext, isAutonomousMode);
            var messages = new List<ChatMessage> { new ChatMessage { Role = ChatMessageRole.User, Content = prompt } };
            var options = new StreamOptions { Temperature = 0.5 };

            try
            {
                var hypothesisText = new StringBuilder();
                await foreach (var chunk in _llmService.StreamAsync(messages, options, cancellationToken))
                {
                    hypothesisText.Append(chunk.Content);
                }
                return ExtractHypotheses(hypothesisText.ToString());
            }
            catch (Exception ex)
            {
                await _logger.WriteErrorAsync($"[gap29_8_6] LLM hypothesis generation failed: {ex.Message}", ex);
                return new List<string>();
            }
        }

        private string BuildHypothesisPrompt(ErrorAnalysisResult errorAnalysis, CodeChange previousChange, string sessionContext, bool isAutonomousMode)
        {
            var filePath = errorAnalysis.FilePath ?? "(unknown)";
            var lineNumber = errorAnalysis.LineNumber?.ToString(CultureInfo.InvariantCulture) ?? "(unknown)";
            var changeFile = previousChange?.FilePath ?? "(unknown)";
            var changeDesc = previousChange?.Description ?? "(unknown)";
            var ctx = sessionContext ?? "(no context)";
            var mode = isAutonomousMode ? "Autonomous (auto-execute)" : "Interactive (user review)";

            var sb = new StringBuilder();
            sb.AppendLine("You are a code debugging assistant analyzing a failure in a C# project during Debug mode refinement.");
            sb.AppendLine();
            sb.AppendLine("ERROR INFORMATION:");
            sb.AppendLine($"- Type: {errorAnalysis.ErrorType}");
            sb.AppendLine($"- Category: {errorAnalysis.Category}");
            sb.AppendLine($"- Message: {errorAnalysis.Message}");
            sb.AppendLine($"- File: {filePath}");
            sb.AppendLine($"- Line: {lineNumber}");
            sb.AppendLine();
            sb.AppendLine("PREVIOUS CHANGE:");
            sb.AppendLine($"- File: {changeFile}");
            sb.AppendLine($"- Description: {changeDesc}");
            sb.AppendLine();
            sb.AppendLine("SESSION CONTEXT:");
            sb.AppendLine(ctx);
            sb.AppendLine();
            sb.AppendLine("Your task:");
            sb.AppendLine("1. Analyze the error and generate 2-3 hypotheses about root causes");
            sb.AppendLine("2. For each hypothesis, explain the likely fix approach");
            sb.AppendLine("3. Return a JSON object with this structure:");
            sb.AppendLine("{");
            sb.AppendLine("  \"hypotheses\": [");
            sb.AppendLine("    \"Hypothesis 1: [description and suggested fix approach]\",");
            sb.AppendLine("    \"Hypothesis 2: [description and suggested fix approach]\"");
            sb.AppendLine("  ]");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.Append($"Focus on actionable, testable hypotheses. Be concise. Mode: {mode}");
            return sb.ToString();
        }

        private List<string> ExtractHypotheses(string llmResponse)
        {
            var hypotheses = new List<string>();

            try
            {
                var jsonMatch = Regex.Match(llmResponse, @"\{[^{}]*""hypotheses""\s*:\s*\[(.+?)\]", RegexOptions.Singleline);
                if (jsonMatch.Success)
                {
                    var jsonText = $"{{\"hypotheses\": [{jsonMatch.Groups[1].Value}]}}";
                    var json = JObject.Parse(jsonText);
                    var hyps = json["hypotheses"]?.ToObject<List<string>>();
                    if (hyps != null && hyps.Any())
                        return hyps;
                }
            }
            catch
            {
            }

            var bulletMatches = Regex.Matches(llmResponse, @"(?:[-*\d\.]+\s+)?(.{20,}).+?(?:—|:|\.(?:\s|$)|$)", RegexOptions.Multiline | RegexOptions.Singleline);
            foreach (Match m in bulletMatches)
            {
                var hyp = m.Groups[1].Value.Trim();
                if (!string.IsNullOrWhiteSpace(hyp) && hyp.Length > 10)
                    hypotheses.Add(hyp);
            }

            return hypotheses.Take(3).ToList();
        }

        private double CalculateConfidenceScore(List<string> hypotheses, ErrorType errorType)
        {
            double score = errorType switch
            {
                ErrorType.Compilation => 0.6,
                ErrorType.TestFailure => 0.5,
                ErrorType.Exception => 0.4,
                _ => 0.2
            };

            if (hypotheses.Count >= 2)
                score += 0.2;
            else if (hypotheses.Count == 1)
                score += 0.1;

            return Math.Min(score, 0.95);
        }

        private CodeChange GenerateRefinedChange(CodeChange originalChange, List<string> hypotheses, ErrorAnalysisResult errorAnalysis)
        {
            return new CodeChange
            {
                ChangeId = Guid.NewGuid().ToString(),
                FilePath = originalChange.FilePath,
                OldContent = originalChange.OldContent,
                NewContent = GenerateRefinedContent(originalChange, hypotheses.FirstOrDefault()),
                Description = $"Refined: {originalChange.Description} (addressing: {hypotheses.FirstOrDefault()})",
                Baseline = originalChange.Baseline,
                Timestamp = DateTime.UtcNow
            };
        }

        private string GenerateRefinedContent(CodeChange originalChange, string topHypothesis)
        {
            var content = originalChange.NewContent ?? originalChange.OldContent ?? "";
            if (topHypothesis?.Contains("null", StringComparison.OrdinalIgnoreCase) ?? false)
            {
                content = Regex.Replace(content, @"(\w+)\.(?=\w)", m => $"({m.Groups[1].Value}?.({m.Groups[1].Value} != null ? \"\" : null) ?? \"{m.Groups[1].Value}\")?.");
            }
            return content;
        }

        private string FormatApproachDescription(List<string> hypotheses, double confidence)
        {
            var topHypothesis = hypotheses.FirstOrDefault() ?? "Unknown";
            var confidencePercent = (confidence * 100).ToString("F0");
            var action = confidence >= 0.6 ? "Auto-apply" : "Review before applying";

            var sb = new StringBuilder();
            sb.AppendLine("Based on error analysis:");
            sb.AppendLine($"- Top hypothesis: {topHypothesis}");
            sb.AppendLine($"- Confidence: {confidencePercent}%");
            sb.AppendLine("- Refinement approach: Address root cause via targeted code modifications");
            sb.Append($"- Recommended action: {action}");

            return sb.ToString();
        }
    }
}
