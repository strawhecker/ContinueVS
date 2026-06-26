namespace ContinueCore.Llm;
public enum LLMConfigurationStatuses
{
    // TS value: "valid"
    VALID,
    // TS value: "missing-api-key"
    MISSING_API_KEY,
    // TS value: "missing-env-secret"
    MISSING_ENV_SECRET
}

public enum NEXT_EDIT_MODELS
{
    // TS value: "mercury-coder"
    MERCURY_CODER,
    // TS value: "instinct"
    INSTINCT
}