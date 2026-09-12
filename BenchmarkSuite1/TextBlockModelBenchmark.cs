using BenchmarkDotNet.Attributes;
using ContinueVS.Core.Types;
using System;

namespace ContinueVS.Benchmarks
{
    [MemoryDiagnoser]
    public class TextBlockModelBenchmark
    {
        private string _simpleText;
        private string _markdownText;
        private string _complexMarkdownText;
        [GlobalSetup]
        public void Setup()
        {
            _simpleText = "This is plain text without any markdown.";
            _markdownText = "This has **bold text** and *italic text* and `code`.";
            _complexMarkdownText = "**Bold** and *italic* and `code` mixed with **more bold** and *more italic* and `more code` repeated.";
        }

        [Benchmark]
        public TextBlockModel SetSimpleText()
        {
            var model = new TextBlockModel();
            model.Text = _simpleText;
            return model;
        }

        [Benchmark]
        public TextBlockModel SetMarkdownText()
        {
            var model = new TextBlockModel();
            model.Text = _markdownText;
            return model;
        }

        [Benchmark]
        public TextBlockModel SetComplexMarkdownText()
        {
            var model = new TextBlockModel();
            model.Text = _complexMarkdownText;
            return model;
        }

        [Benchmark]
        public void MultiplePropertyUpdates()
        {
            var model = new TextBlockModel();
            for (int i = 0; i < 10; i++)
            {
                model.Text = $"Update {i}: **bold** and *italic*";
                model.ChunkIndex = i;
                model.IsCodeBlock = i % 2 == 0;
            }
        }
    }
}
