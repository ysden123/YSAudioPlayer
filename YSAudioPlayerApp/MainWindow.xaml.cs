using Microsoft.Win32;
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

        private void OpenFolderMenuItem_Click(object sender, RoutedEventArgs e)
        {
            OpenFolderDialog openFolderDialog = new()
            {
                Title = "Select a folder with audio files"
            };


            if (openFolderDialog.ShowDialog() ?? false)
            {
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

                    this.ListView_Tracks.ItemsSource = _tracks;
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
    }
}