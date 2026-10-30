
namespace DevTools
{
    public class ProcessInfo
    {
        public string ProcessName { get; set; } = string.Empty;

        public int ProcessId { get; set; }

        public double MemoryMb { get; set; }

        public string WindowTitle { get; set; } = string.Empty;
    }
}
