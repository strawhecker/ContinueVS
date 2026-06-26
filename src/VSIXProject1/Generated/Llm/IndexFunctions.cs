namespace ContinueCore.Llm;
public static partial class IndexFunctions
{
    public static bool isModelInstaller(object provider)
    {
        return provider && provider.installModel is System.Delegate && provider.isInstallingModel is System.Delegate;
    }
}