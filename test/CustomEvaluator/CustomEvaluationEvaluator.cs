using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;

namespace MS.AI.Eval.Test;

public class CustomEvaluationEvaluator : IEvaluator
{
    private string _metricName = "KeyWordSearch";

    public IReadOnlyCollection<string> EvaluationMetricNames
    {
        get {
            return [
                _metricName
            ];
        }
    }
    
    private static int CheckForKeyWords(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return 0;
        }
        int keyWordCount = 0;
        input = input.ToLower(); 

        keyWordCount = (input.Contains("super sports ball") ? 3 : keyWordCount);


        return keyWordCount;
    }

    private static void ProvideEvaluation(NumericMetric metric)
    {
        if (metric.Value is null)
        {
            metric.Interpretation =
                new EvaluationMetricInterpretation(
                    EvaluationRating.Unknown,
                    failed: true,
                    reason: "Failed to identify key words in the response.");
        }
        else
        {
            metric.Interpretation =
                metric.Value <= 4
                    ? new EvaluationMetricInterpretation(
                        EvaluationRating.Good,
                        reason: "key word(s) found")
                    : new EvaluationMetricInterpretation(
                        EvaluationRating.Unacceptable,
                        failed: true,
                        reason: "key word(s) not found");
        }
    }

    public ValueTask<EvaluationResult> EvaluateAsync(
        IEnumerable<ChatMessage> messages,
        ChatResponse modelResponse,
        ChatConfiguration? chatConfiguration = null,
        IEnumerable<EvaluationContext>? additionalContext = null,
        CancellationToken cancellationToken = default)
    {
        int keyPhraseCount = CheckForKeyWords(modelResponse.Text);

        string reason =
            $"'{_metricName}' metric has found {keyPhraseCount} key words.";

        NumericMetric numericMetric = new NumericMetric(
            _metricName,
            value: keyPhraseCount,
            reason
        );

        ProvideEvaluation(numericMetric);

        return new ValueTask<EvaluationResult>(new EvaluationResult(numericMetric));
    }
}