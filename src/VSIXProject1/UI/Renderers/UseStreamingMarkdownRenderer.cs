namespace ContinueVS.UI.Renderers
{
    /// <summary>
    /// gap88: A/B feature flag for the unified chat card renderer.
    ///
    /// Default OFF: the unified <see cref="StreamingMarkdownRenderer"/> stays
    /// hidden and the legacy split (StreamingReasoningRenderer for user/thinking,
    /// MarkdownBlockRenderer for assistant) is unchanged — zero behavior change.
    ///
    /// When set to true, the unified renderer takes over the hosted roles so it
    /// can be validated before the legacy renderers are retired.
    /// </summary>
    public static class UseStreamingMarkdownRenderer
    {
        /// <summary>
        /// Toggle to enable the unified renderer path. Defaults to false.
        /// </summary>
        public static bool IsEnabled = false;
    }
}
