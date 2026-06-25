using ContinueVS.IPC;
using ContinueVS.UI;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ContinueVS.Handlers.Ide
{
    internal sealed class GetUniqueIdHandler : IMessageHandler
    {
        private readonly ContinueToolWindowControl _control;

        private static string _cachedId = null;

        public GetUniqueIdHandler(ContinueToolWindowControl control)
        {
            _control = control;
        }

        public Task HandleAsync(Message message, CancellationToken cancellationToken)
        {
            var id = GetOrCreateId();
            _control.SendReplyToGui(message.MessageType, message.MessageId, id);
            return Task.CompletedTask;
        }

        private static string GetOrCreateId()
        {
            if (_cachedId != null)
                return _cachedId;

            var filePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ContinueVS",
                "unique-id.txt");

            if (File.Exists(filePath))
            {
                var content = File.ReadAllText(filePath).Trim();
                if (!string.IsNullOrEmpty(content))
                {
                    _cachedId = content;
                    return _cachedId;
                }
            }

            var newId = Guid.NewGuid().ToString();
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            File.WriteAllText(filePath, newId);
            _cachedId = newId;
            return _cachedId;
        }
    }
}
