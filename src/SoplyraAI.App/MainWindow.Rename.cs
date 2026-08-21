using System.Windows.Controls;
using System.Windows.Threading;
using SoplyraAI.Models;

namespace SoplyraAI;

public partial class MainWindow
{
    private void RenameGuide_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is not Button { Tag: GuideSession session }) return;

        if (_recorder.IsRecording && session.Id != _current.Id)
            StopRecording();

        if (session.Id != _current.Id)
        {
            _sessions.Save(_current);
            _current = session;
            BindCurrent();
            RefreshSessions(selectCurrent: true);
        }

        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            GuideTitle.Focus();
            GuideTitle.SelectAll();
        }));
    }
}
