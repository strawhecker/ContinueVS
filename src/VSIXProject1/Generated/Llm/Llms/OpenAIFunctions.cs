namespace ContinueCore.Llm.Llms;
public static partial class OpenAIFunctions
{
    public static bool isChatOnlyModel(string model)
    {
        return model.startsWith("gpt") || model.startsWith("o");
    }
}