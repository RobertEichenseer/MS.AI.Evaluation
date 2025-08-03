namespace MS.AI.Eval.Test;

using DotNetEnv;
using System.Diagnostics;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.AI.Evaluation.Quality;
using Microsoft.Extensions.AI.Evaluation.Reporting;
using Microsoft.Extensions.AI.Evaluation.Reporting.Storage;
using MS.AI.Eval.SDK;
using Azure.AI.Inference;
using Azure;

public class SDK_Test
{

    static string _configurationFile = @"..\..\..\..\config\config.env";
    static string _oAiApiKey = "";
    static string _oAiEndpoint = "";
    static string _oAiChatDeployment = "";
    static string _reportingPath = "";

    CustomEvaluation _customEvaluation;

    //********************************************************************************
    // Unit Test Constructor
    //********************************************************************************    
    public SDK_Test()
    {

        Env.Load(_configurationFile);

        _oAiApiKey = Environment.GetEnvironmentVariable("AOAI_APIKEY") ?? "";
        _oAiChatDeployment = Environment.GetEnvironmentVariable("CHAT_DEPLOYMENTNAME") ?? "";
        _oAiEndpoint = Environment.GetEnvironmentVariable("AOAI_ENDPOINT") ?? "";
        _oAiEndpoint = $"{_oAiEndpoint}openai/deployments/{_oAiChatDeployment}/";
        _reportingPath = Environment.GetEnvironmentVariable("REPORTING_PATH") ?? "";

        _customEvaluation = new CustomEvaluation(_oAiApiKey, _oAiEndpoint, _oAiChatDeployment);

    }

    //********************************************************************************
    // Unit Tests
    //********************************************************************************
    [Fact]
    public async Task SingleEvaluator_Dev()
    {
        //Get ChatMessages and ChatResponse from functionality which should be tested
        IList<ChatMessage> chatMessages = _customEvaluation.GetChatMessages();
        ChatResponse chatResponse = await _customEvaluation.GetChatResponse();

        //Evaluate response
        IChatClient chatClient = GetEvaluationChatClient();
        ChatConfiguration chatConfiguration = new ChatConfiguration(chatClient);


        IEvaluator coherenceEvaluator = new CoherenceEvaluator();

        EvaluationResult evaluationResult = await coherenceEvaluator.EvaluateAsync(
            chatMessages,
            chatResponse,
            chatConfiguration
        );

        bool evaluationSuccess = true;
        foreach (string metricName in coherenceEvaluator.EvaluationMetricNames)
        {
            Debug.WriteLine("");
            EvaluationMetric evaluationMetric = evaluationResult.Get<NumericMetric>(metricName);
            ShowEvaluationResult(evaluationMetric);

            if (evaluationMetric is NumericMetric numericMetric
                    && numericMetric.Value < 3
            )
            {
                evaluationSuccess = false;
                break;
            }
        }

        Assert.True(evaluationSuccess, "Evaluation did not meet the expected criteria.");
    }


    [Fact]
    public async Task MultipleEvaluator_Dev()
    {
        //Get ChatMessages and ChatResponse from functionality which should be tested
        IList<ChatMessage> chatMessages = _customEvaluation.GetChatMessages();
        ChatResponse chatResponse = await _customEvaluation.GetChatResponse();

        //Evaluate response using LLM
        ChatConfiguration chatConfiguration = new ChatConfiguration(GetEvaluationChatClient());

        IEvaluator coherenceEvaluator = new CoherenceEvaluator();
        IEvaluator relevanceEvaluator = new RelevanceEvaluator();

        IEvaluator compositeEvaluator = new CompositeEvaluator(coherenceEvaluator, relevanceEvaluator);
        EvaluationResult evaluationResult = await compositeEvaluator.EvaluateAsync(chatMessages, chatResponse, chatConfiguration);

        NumericMetric coherenceMetric = evaluationResult.Get<NumericMetric>(CoherenceEvaluator.CoherenceMetricName);
        NumericMetric relevanceMetric = evaluationResult.Get<NumericMetric>(RelevanceEvaluator.RelevanceMetricName);

        ShowEvaluationResult(coherenceMetric);
        ShowEvaluationResult(relevanceMetric);
        
        Assert.True(
            (coherenceMetric.Value >= 3 && relevanceMetric.Value >= 3),
            "Evaluation did not meet the required criteria."
        );
        
    }

    [Fact]
    public async Task Reporting_Dev()
    {
        //Get ChatMessages and ChatResponse from functionality which should be tested
        IList<ChatMessage> chatMessages = _customEvaluation.GetChatMessages();
        ChatResponse chatResponse = await _customEvaluation.GetChatResponse();

        //Evaluate response using LLM
        ChatConfiguration chatConfiguration = new ChatConfiguration(GetEvaluationChatClient());

        List<IEvaluator> reportingEvaluators = new List<IEvaluator>()
        {
            new CoherenceEvaluator(),
            new RelevanceEvaluator(),
        };

        string executionName = $"Execution-{DateTime.UtcNow:yyyy-MM-dd-HH-mm-ss}";
        ReportingConfiguration reportingConfiguration = DiskBasedReportingConfiguration.Create(
            storageRootPath: _reportingPath,
            evaluators: reportingEvaluators,
            chatConfiguration: chatConfiguration,
            enableResponseCaching: true,
            executionName: executionName
        );

        string scenarioName = "SuperSportsBallEvaluation";
        await using ScenarioRun scenarioRun =
            await reportingConfiguration.CreateScenarioRunAsync(
                scenarioName
        );

        EvaluationResult evaluationResult = await scenarioRun.EvaluateAsync(chatMessages, chatResponse);
        
        bool evaluationSuccess = true;
        foreach (IEvaluator evaluator in reportingEvaluators)
        {
            foreach (string metricName in evaluator.EvaluationMetricNames)
            {
                EvaluationMetric evaluationMetric = evaluationResult.Get<NumericMetric>(metricName);
                ShowEvaluationResult(evaluationMetric);
                if (
                    evaluationMetric is NumericMetric numericMetric
                    && numericMetric.Value < 3
                )
                {
                    evaluationSuccess = false;
                    break;
                }
            }
        }

        Assert.True(
            evaluationSuccess,
            "Evaluation did not meet the expected criteria."
        );
    }

    [Fact]
    public async Task CustomEvaluator_Dev()
    {
        //Get ChatMessages and ChatResponse from functionality which should be tested
        IList<ChatMessage> chatMessages = _customEvaluation.GetChatMessages();
        ChatResponse chatResponse = await _customEvaluation.GetChatResponse();

        //Evaluate response using custom evaluator
        IEvaluator customResultEvaluator = new CustomEvaluationEvaluator();
        EvaluationResult evaluationResult = await customResultEvaluator.EvaluateAsync(
            chatMessages,
            chatResponse
        );

        string? evaluationName = customResultEvaluator.EvaluationMetricNames.FirstOrDefault();
        NumericMetric wordCountMetric = evaluationResult.Get<NumericMetric>(evaluationName??"");
        ShowEvaluationResult(wordCountMetric);

        Assert.True(
            wordCountMetric.Value != null && wordCountMetric.Value != 0,
            "Expected key words not found"
        );
    }   

    //********************************************************************************
    // Helper Methods
    //********************************************************************************  
    private IChatClient GetEvaluationChatClient()
    {
        IChatClient chatClient = new ChatCompletionsClient(
            new Uri(_oAiEndpoint),
            new AzureKeyCredential(_oAiApiKey)
        )
        .AsIChatClient(_oAiChatDeployment);

        chatClient = chatClient.AsBuilder().UseFunctionInvocation().Build();

        return chatClient;
    }

    private void ShowEvaluationResult(EvaluationMetric evaluationMetric)
    {
        Debug.WriteLine($"\tEvaluation Name: {evaluationMetric.Name}");
        Debug.WriteLine($"\tEvaluation Reason: {evaluationMetric.Reason}");

        switch (evaluationMetric)
        {
            case NumericMetric numericValue:
                Debug.WriteLine($"\tNumeric Metric Value: {numericValue.Value}");
                break;
            case StringMetric stringValue:
                Debug.WriteLine($"\tString Metric Value: {stringValue.Value}");
                break;
        }

        if (evaluationMetric.Diagnostics?.Any() == true)
        {
            Debug.WriteLine(
                $"\tEvaluation Diagnostics: {string.Join(
                    ", ",
                    evaluationMetric.Diagnostics.Select(diagnostic => diagnostic.Message)
                )}"
            );
        }
    }

}
