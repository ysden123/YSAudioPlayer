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

        private string _appFolder;

        private readonly string _baseTitle;

        private readonly ObservableCollection<Track> _tracks = [];

        private WaveOut? _outputDevice;
        private AudioFileReader? _fileReader;
        private PlayerState _playerState = PlayerState.Stopped;
        private PlayerAction _lastAction = PlayerAction.Stop;
        private LastPosition? _lastPosition;
        private static readonly string _lastPositionFileName = "lastposition.json";

        public MainWindow()
        {
            _appFolder = YSCommon.Utils.GetAssemblyFolderInLocalData("ysaudioplayerapp");
#if DEBUG
            string fileName = Path.Combine(_appFolder, "logs", "ysaudioplayerapp-debug.log");
            Log.Logger = new LoggerConfiguration()
               .MinimumLevel.Debug()
               .Enrich.WithThreadId()
               .WriteTo.File(fileName,
               rollingInterval: RollingInterval.Month,
               outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {SourceContext} [{ThreadId}] {Message:lj}{NewLine}{Exception}")
           .CreateLogger();
#else
            string fileName = Path.Combine(_appFolder, "logs", "ysaudioplayerapp.log");
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

            RestoreLastPosition();
        }

        private void RestoreLastPosition()
        {
            try
            {
                using (var readStream = new StreamReader(Path.Combine(_appFolder, _lastPositionFileName)))
                {
                    var json = readStream.ReadToEnd();
                    _lastPosition = LastPosition.FromJson(json);
                }


                FillAndSetPosition(_lastPosition?.FolderName ?? string.Empty, _lastPosition);
            }
            catch(FileNotFoundException)
            {
                _logger.Warning("Last position file not found. Starting with no last position.");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error restoring last window position.");
            }
        }

        private void SaveLastPosition()
        {
            try
            {
                if (_lastPosition != null)
                {
                    string json = _lastPosition.ToJson();
                    Directory.CreateDirectory(_appFolder);
                    using var writeStream = new StreamWriter(Path.Combine(_appFolder, _lastPositionFileName), false);
                    writeStream.Write(json);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error saving last window position.");
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
                FillAndSetPosition(openFolderDialog.FolderName, null);
            }
        }

        private void FillAndSetPosition(string folderName, LastPosition? lastPosition)
        {
            _outputDevice?.Stop();
            _playerState = PlayerState.Stopped;
            _tracks.Clear();

            try
            {
                var filePaths = (from filePath in Directory.EnumerateFiles(folderName, "*.mp3", SearchOption.TopDirectoryOnly)
                                 select filePath).ToList();
                string? album = null;

                foreach (var filePath in filePaths)
                {
                    try
                    {
                        var tfile = TagLib.File.Create(filePath);

                        album ??= tfile.Tag.Album;

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

                Track? savedTrack = (from track in _tracks
                                  where track.FilePath == lastPosition?.FilePath
                                  select track).FirstOrDefault();

                ListView_Tracks.SelectedItem = _tracks.Count > 0 ? (savedTrack ?? _tracks[0]) : null;

                UpdateMenuStatus();

                if (lastPosition == null)
                {
                    _lastPosition = new LastPosition()
                    {
                        FilePath = ListView_Tracks.SelectedItem is Track selectedTrack ? selectedTrack.FilePath : null,
                        CurrentTime = TimeSpan.Zero,
                    };
                    SaveLastPosition();
                }
                else
                {
                    _lastPosition = lastPosition;
                    var trackToSelect = _tracks.FirstOrDefault(t => t.FilePath == lastPosition.FilePath);
                    if (trackToSelect != null)
                    {
                        ListView_Tracks.SelectedItem = trackToSelect;
                        ListView_Tracks.ScrollIntoView(trackToSelect);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error enumerating files in folder: {0}", folderName);
            }
        }
        
        private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void SelectedTrackChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            UpdateMenuStatus();
            var selectedTrack = ListView_Tracks.SelectedItem as Track;
            if (selectedTrack != null)
            {
                _lastPosition?.FilePath = selectedTrack.FilePath;
                _lastPosition?.CurrentTime = TimeSpan.Zero;
            }
            else
            {
                _lastPosition?.FilePath = null;
                _lastPosition?.CurrentTime = TimeSpan.Zero;
            }
            SaveLastPosition();
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
            int selectedIndex = ListView_Tracks.SelectedIndex;

            if (selectedIndex == -1)
                return;

            _logger.Debug("Selected track to play: {0}, file path: {1}", _tracks[selectedIndex].FileName, _tracks[selectedIndex].FilePath);

            try
            {
                _logger.Debug("For track (1): {0}, _lastPosition: {1}", _tracks[selectedIndex].FilePath, _lastPosition);
                if (_outputDevice?.PlaybackState == PlaybackState.Playing)
                {
                    _outputDevice?.Dispose();
                    _fileReader?.Close();
                }

                _logger.Debug("For track (2): {0}, _lastPosition: {1}", _tracks[selectedIndex].FilePath, _lastPosition);
                _outputDevice = new WaveOut();
                _fileReader = new AudioFileReader(_tracks[selectedIndex].FilePath)
                {
                    CurrentTime = _lastPosition?.CurrentTime ?? TimeSpan.Zero
                };

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
                var selectedTrack = ListView_Tracks.SelectedItem as Track;
                if (selectedTrack != null)
                {
                    _lastPosition?.CurrentTime = _fileReader?.CurrentTime ?? TimeSpan.Zero;
                }
                SaveLastPosition();

                _outputDevice?.Stop();
                SetCurrentState(PlayerAction.Stop);
                UpdateMenuStatus();
            }
        }
    }
}