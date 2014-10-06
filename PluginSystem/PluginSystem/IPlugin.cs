namespace VT100.PluginSystem
{
    public interface IPlugin
    {
        bool IsRunning
        { get; }

        bool Start(VTconsole Console);
        void Stop();
        void Block();
        bool Block(int Timeout);
        void RecMessage(IPlugin source, string Message);
        void RecMessage(IPlugin source, byte[] Message, int index, int Length);
    }
}
