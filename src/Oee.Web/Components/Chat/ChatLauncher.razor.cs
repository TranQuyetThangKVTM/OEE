namespace Oee.Web.Components.Chat;

public partial class ChatLauncher
{
    private readonly List<ChatMessage> _messages = [];
    private bool _open;
    private bool _expanded;

    private void Toggle()
    {
        if (_open) Close();
        else _open = true;
    }

    private void Close()
    {
        _open = false;
        _expanded = false;
    }
}
