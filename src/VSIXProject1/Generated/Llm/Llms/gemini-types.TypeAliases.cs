namespace ContinueCore.Llm.Llms;
/* TODO type alias: GeminiObjectSchemaType = | "TYPE_UNSPECIFIED"
  | "STRING"
  | "NUMBER"
  | "INTEGER"
  | "BOOLEAN"
  | "ARRAY"
  | "OBJECT" */
public static class GeminiObjectSchemaTypeAlias
{
}

/* TODO type alias: GeminiTextContentPart = {
  text: string;
} */
public static class GeminiTextContentPartAlias
{
}

/* TODO type alias: GeminiInlineDataContentPart = {
  inlineData: {
    mimeType: string;
    data: string;
  };
} */
public static class GeminiInlineDataContentPartAlias
{
}

/* TODO type alias: GeminiFunctionCallContentPart = {
  functionCall: {
    id?: string;
    name: string;
    args: JSONSchema7Object;
  };
  thoughtSignature?: string;
} */
public static class GeminiFunctionCallContentPartAlias
{
}

/* TODO type alias: GeminiFunctionResponseContentPart = {
  functionResponse: {
    id?: string;
    name: string;
    response: JSONSchema7Object;
  };
} */
public static class GeminiFunctionResponseContentPartAlias
{
}

/* TODO type alias: GeminiFileDataContentPart = {
  fileData: {
    fileUri: string;
    mimeType: string; // See possible values here: https://cloud.google.com/vertex-ai/generative-ai/docs/model-reference/inference#filedata
  };
} */
public static class GeminiFileDataContentPartAlias
{
}

/* TODO type alias: GeminiExecutableCodeContentPart = {
  executableCode: {
    language: "PYTHON" | "LANGUAGE_UNSPECIFIED";
    code: string;
  };
} */
public static class GeminiExecutableCodeContentPartAlias
{
}

/* TODO type alias: GeminiCodeExecutionResultContentPart = {
  codeExecutionResult: {
    outcome:
      | "OUTCOME_UNSPECIFIED"
      | "OUTCOME_OK"
      | "OUTCOME_FAILED"
      | "OUTCOME_DEADLINE_EXCEEDED";
    output: string;
  };
} */
public static class GeminiCodeExecutionResultContentPartAlias
{
}

/* TODO type alias: GeminiChatContentPart = | GeminiTextContentPart
  | GeminiInlineDataContentPart
  | GeminiFunctionCallContentPart
  | GeminiFunctionResponseContentPart
  | GeminiFileDataContentPart
  | GeminiExecutableCodeContentPart
  | GeminiCodeExecutionResultContentPart */
public static class GeminiChatContentPartAlias
{
}

/* TODO type alias: GeminiChatResponse = | GeminiChatResponseError
  | GeminiChatResponseSuccess */
public static class GeminiChatResponseAlias
{
}