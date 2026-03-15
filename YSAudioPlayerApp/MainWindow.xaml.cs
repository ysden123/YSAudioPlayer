using Microsoft.Win32;
using NAudio.Wave;
using Serilog;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;

namespace YSAudioPlayerApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static readonly ILogger _logger = Log.ForContext<MainWindow>();

        private readonly string _baseTitle;

        private readonly ObservableCollection<Track> _tracks = [];

        private WaveOut? _outputDevice;
        private AudioFileReader? _fileReader;
        private PlayerState _playerState = PlayerState.Stopped;
        private PlayerAction _lastAction = PlayerAction.Stop;

        public MainWindow()
        {
            var folder = YSCommon.Utils.GetAssemblyFolderInLocalData("ysaudioplayerapp");
#if DEBUG
            string fileName = Path.Combine(folder, "logs", "ysaudioplayerapp-debug.log");
            Log.Logger = new LoggerConfiguration()
               .MinimumLevel.Debug()
               .Enrich.WithThreadId()
               .WriteTo.File(fileName,
               rollingInterval: RollingInterval.Month,
               outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {SourceContext} [{ThreadId}] {Message:lj}{NewLine}{Exception}")
           .CreateLogger();
#else
            string fileName = Path.Combine(folder, "logs", "ysaudioplayerapp.log");
            Log.Logger = new LoggerConfiguration()
               .MinimumLevel.Error()
               .Enrich.WithThreadId()
               .WriteTo.File(fileName,
               rollingInterval: RollingInterval.Month,
               outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {SourceContext} [{ThreadId}] {Message:lj}{NewLine}{Exception}")
           .CreateLogger();
#endif
            InitializeComponent();

            _baseTitle = Title;

            System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
            System.Diagnostics.FileVersionInfo fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(assembly.Location);
            if (fvi != null && fvi.FileVersion != null)
            {
                string version = fvi.FileVersion;
                Title = $"{Title} {version}";
            }
        }

        private void SetCurrentState(PlayerAction action)
        {
            _playerState = action switch
            {
                PlayerAction.Play => PlayerState.Playing,
                PlayerAction.Stop => PlayerState.Stopped,
                _ => throw new NotImplementedException($"{action} is not supported yet."),
            };
        }

        private void UpdateMenuStatus()
        {
            int selectedIndex = ListView_Tracks.SelectedIndex;
            switch (_playerState)
            {
                case PlayerState.Playing:
                    MenuItem_PlayTrack.IsEnabled = true;
                    MenuItem_StopTrack.IsEnabled = true;
                    break;
                case PlayerState.Stopped:
                    MenuItem_PlayTrack.IsEnabled = selectedIndex != -1;
                    MenuItem_StopTrack.IsEnabled = false;
                    break;
                default:
                    throw new NotImplementedException($"Player state {_playerState} is not supported yet.");
            }
        }

        private void OpenFolderMenuItem_Click(object sender, RoutedEventArgs e)
        {
            OpenFolderDialog openFolderDialog = new()
            {
                Title = "Select a folder with audio files"
            };


            if (openFolderDialog.ShowDialog() ?? false)
            {
                _outputDevice?.Stop();
                _playerState = PlayerState.Stopped;
                _tracks.Clear();

                try
                {
                    var filePaths = (from filePath in Directory.EnumerateFiles(openFolderDialog.FolderName, "*.mp3", SearchOption.TopDirectoryOnly)
                                     select filePath).ToList();
                    string? album = null;

                    foreach (var filePath in filePaths)
                    {
                        try
                        {
                            var tfile = TagLib.File.Create(filePath);

                            if (album == null)
                            {
                                album = tfile.Tag.Album;
                            }

                            _tracks.Add(new Track()
                            {
                                FileName = Path.GetFileName(filePath),
                                FilePath = filePath,
                                Title = tfile.Tag.Title,
                                Duration = tfile.Properties.Duration
                            });
                        }
                        catch (Exception ex)
                        {
                            _logger.Error(ex, "Error processing file: {0}", filePath);
                        }
                    }

                    System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
                    System.Diagnostics.FileVersionInfo fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(assembly.Location);
                    if (fvi != null && fvi.FileVersion != null)
                    {
                        string version = fvi.FileVersion;
                        Title = $"{_baseTitle} {version}: {album}";
                    }

                    ListView_Tracks.ItemsSource = _tracks;
                    ListView_Tracks.SelectedItem = _tracks.Count > 0 ? _tracks[0] : null;

                    UpdateMenuStatus();
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error enumerating files in folder: {0}", openFolderDialog.SafeFolderName);
                }
            }
        }

        private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void SelectedTrackChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            UpdateMenuStatus();
        }

        private void MenuItem_PlayTrack_Click(object sender, RoutedEventArgs e)
        {
            _lastAction = PlayerAction.Play;
            DoCommandPlay();
        }

        private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
        {
            _outputDevice?.Dispose();

            if (_lastAction == PlayerAction.Play && ListView_Tracks.SelectedIndex < ListView_Tracks.Items.Count - 1)
            {
                ListView_Tracks.SelectedIndex++;
                DoCommandPlay();
            }
            else
            {
                DoCommandStop();
            }
        }

        private void MenuItem_StopTrack_Click(object sender, RoutedEventArgs e)
        {
            _lastAction = PlayerAction.Stop;
            DoCommandStop();
        }

        private void DoCommandPlay()
        {
            // TODO: Implement track playing logic here
            int selectedIndex = ListView_Tracks.SelectedIndex;

            if (selectedIndex == -1)
                return;

            _logger.Debug("Selected track to play: {0}, file path: {1}", _tracks[selectedIndex].FileName, _tracks[selectedIndex].FilePath);

            try
            {
                if (_outputDevice?.PlaybackState == PlaybackState.Playing)
                {
                    _outputDevice?.Dispose();
                    _fileReader?.Close();
                }

                _outputDevice = new WaveOut();
                _fileReader = new AudioFileReader(_tracks[selectedIndex].FilePath);

                // todo: dispose outputDevice when playback stops
                _outputDevice?.PlaybackStopped += OnPlaybackStopped;

                _outputDevice?.Init(_fileReader);
                _outputDevice?.Play();
                SetCurrentState(PlayerAction.Play);
                UpdateMenuStatus();
                //_outputDevice?.Pause(); // Start paused, user can resume when ready
                //_outputDevice?.Stop();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error playing track: {0}", _tracks[selectedIndex].FilePath);
            }

        }
        private void DoCommandStop()
        {

            if (_playerState == PlayerState.Playing)
            {
                _outputDevice?.Stop();
                SetCurrentState(PlayerAction.Stop);
                UpdateMenuStatus();
            }
        }
    }
}