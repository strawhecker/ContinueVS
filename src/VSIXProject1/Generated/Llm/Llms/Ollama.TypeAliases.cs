namespace ContinueCore.Llm.Llms;
/* TODO type alias: OllamaChatMessage = {
  role: ChatMessageRole;
  content: string;
  images?: string[] | null;
  thinking?: string;
  tool_calls?: {
    function: {
      name: string;
      arguments: JSONSchema7Object;
    };
  }[];
} */
public static class OllamaChatMessageAlias
{
}

/* TODO type alias: OllamaBaseResponse = {
  model: string;
  created_at: string;
} & (
  | {
      done: false;
    }
  | {
      done: true;
      done_reason: string;
      total_duration: number; // Time spent generating the response in nanoseconds
      load_duration: number; // Time spent loading the model in nanoseconds
      prompt_eval_count: number; // Number of tokens in the prompt
      prompt_eval_duration: number; // Time spent evaluating the prompt in nanoseconds
      eval_count: number; // Number of tokens in the response
      eval_duration: number; // Time spent generating the response in nanoseconds
      context: number[]; // An encoding of the conversation used in this response; can be sent in the next request to keep conversational memory
    }
) */
public static class OllamaBaseResponseAlias
{
}

/* TODO type alias: OllamaErrorResponse = {
  error: string;
} */
public static class OllamaErrorResponseAlias
{
}

/* TODO type alias: N8nChatReponse = {
  type: string;
  content?: string;
  metadata: {
    nodeId: string;
    nodeName: string;
    itemIndex: number;
    runIndex: number;
    timestamps: number;
  };
} */
public static class N8nChatReponseAlias
{
}

/* TODO type alias: OllamaRawResponse = | OllamaErrorResponse
  | (OllamaBaseResponse & {
      response: string; // the generated response
    }) */
public static class OllamaRawResponseAlias
{
}

/* TODO type alias: OllamaChatResponse = | OllamaErrorResponse
  | (OllamaBaseResponse & {
      message: OllamaChatMessage;
    })
  | N8nChatReponse */
public static class OllamaChatResponseAlias
{
}