namespace ContinueCore.Util;
public static partial class ErrorsFunctions
{
    public static object getRootCause(object err)
    {
        if (err.cause)
        {
            return getRootCause(err.cause);
        }

        return err;
    }
}