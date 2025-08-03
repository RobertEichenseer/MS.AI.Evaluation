using Azure;
using Azure.AI.Inference;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.AI.Evaluation.Quality;
using Microsoft.Extensions.AI.Evaluation.Safety;
using Microsoft.Extensions.AI.Evaluation.Reporting;
using System.Diagnostics;

namespace MS.AI.Eval.SDK;

public class CustomEvaluation
{
    static string _oAiApiKey = "";
    static string _oAiEndpoint = "";
    static string _oAiChatDeployment = "";

    IChatClient _chatClient;

    public CustomEvaluation(string apiKey, string endpoint, string chatDeployment)
    {
        _oAiApiKey = apiKey;
        _oAiEndpoint = endpoint;
        _oAiChatDeployment = chatDeployment;

        _chatClient = GetChatClient();
    }

    public IChatClient GetChatClient()
    {
        IChatClient chatClient = new ChatCompletionsClient(
            new Uri(_oAiEndpoint),
            new AzureKeyCredential(_oAiApiKey)
        )
        .AsIChatClient(_oAiChatDeployment);

        chatClient = chatClient.AsBuilder().UseFunctionInvocation().Build();

        return chatClient;
    }

    public async Task<ChatResponse> GetChatResponse()
    {
        await Task.CompletedTask;

        IList<ChatMessage> chatMessages = GetChatMessages();
        ChatOptions chatOptions = new ChatOptions
        {
            Temperature = 0.0f,
            ResponseFormat = ChatResponseFormat.Text
        };

        return _chatClient.GetResponseAsync(chatMessages, chatOptions).Result;
    }

    public IList<ChatMessage> GetChatMessages()
    {
        return new List<ChatMessage> {
            new ChatMessage(
                Microsoft.Extensions.AI.ChatRole.System,
                """
                You provide answers related to sport events.
                You keep your responses concise and response with maximal two short sentences.
                The Flying Dolphins Munich won the Super Sports Ball in 2025.
                """
            ),
            new ChatMessage(
                Microsoft.Extensions.AI.ChatRole.User,
                "Who won the Super Sport Ball 2025?"
            )
        };
    }
}
