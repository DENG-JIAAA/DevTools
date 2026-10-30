
namespace DevTools
{
    public class PortInfo
    {
        public string LocalAddress { get; set; } = string.Empty;

        public int Port { get; set; }

        public int ProcessId { get; set; }

        public string ProcessName { get; set; } = string.Empty;

        public string Status { get; set; } = "LISTENING";
    }
}
