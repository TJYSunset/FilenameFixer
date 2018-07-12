using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using FilenameFixer.Annotations;

namespace FilenameFixer
{
    /// <summary>
    ///     Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : INotifyPropertyChanged
    {
        private Encoding _actualEncoding;
        private Encoding _readAsEncoding;

        private State CurrentState = State.Idle;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += OnLoaded;

            ActualCodePage = "932"; // Shift-JIS
            ReadAsCodePage = Encoding.Default.CodePage.ToString(); // "OEM"
        }

        public string TargetPath
        {
            get => (string) GetValue(TargetPathProperty);
            set => SetValue(TargetPathProperty, value);
        }

        public string ActualCodePage
        {
            get => (string) GetValue(ActualCodePageProperty);
            set => SetValue(ActualCodePageProperty, value);
        }

        public string ReadAsCodePage
        {
            get => (string) GetValue(ReadAsCodePageProperty);
            set => SetValue(ReadAsCodePageProperty, value);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            TargetPathTextBox.Focus();
        }

        private void RefreshGui(object sender, EventArgs e)
        {
            RefreshGui();
        }

        private static string FixFileName(string source, Encoding actualEncoding, Encoding readAsEncoding)
        {
            return actualEncoding.GetString(readAsEncoding.GetBytes(source));
        }

        private void LoadFileList(object sender, EventArgs e)
        {
            IEnumerable<string> EnumeratePath(string path)
            {
                return Directory.GetFileSystemEntries(path)
                    .Concat(Directory.GetDirectories(path).SelectMany(EnumeratePath));
            }

            try
            {
                if (!Directory.Exists(TargetPath))
                {
                    Preview = "Provided path either does not exist or is not a directory at all.";
                    return;
                }

                _actualEncoding = Encoding.GetEncoding(int.Parse(ActualCodePage));
                _readAsEncoding = Encoding.GetEncoding(int.Parse(ReadAsCodePage));
                Preview = "Files & directories will be renamed to the following:\n\n" +
                          string.Join("\n", EnumeratePath(TargetPath)
                              .Select(Path.GetFileName)
                              .Select(x => FixFileName(x, _actualEncoding, _readAsEncoding))
                          );

                CurrentState = State.Ready;
            }
            catch (Exception ex)
            {
                Preview = $"Exception encountered. Maybe you entered a wrong code page.\n\n{ex}";
            }
            finally
            {
                RefreshGui();
            }
        }

        private void Fix(object sender, EventArgs e)
        {
            void FixDirectory(string path)
            {
                string ToFixedName(string x)
                {
                    return Path.Combine(Path.GetDirectoryName(x),
                        FixFileName(Path.GetFileName(x), _actualEncoding, _readAsEncoding));
                }

                foreach (var x in Directory.GetFiles(path))
                {
                    var newName = ToFixedName(x);
                    if (newName == x) continue;
                    Preview += $"Renaming {x} -> {newName}\n\n";
                    File.Move(x, newName);
                }

                foreach (var x in Directory.GetDirectories(path))
                {
                    var newName = ToFixedName(x);
                    if (newName != x)
                    {
                        Preview += $"Renaming {x} -> {newName}\n\n";
                        Directory.Move(x, newName);
                    }

                    FixDirectory(newName);
                }
            }

            Preview = "";

            try
            {
                FixDirectory(TargetPath);
                Preview = $"Success.\n\n===== Logs =====\n\n{Preview}";
            }
            catch (Exception ex)
            {
                Preview += $"Exception encountered. \n\n{ex}";
            }
            finally
            {
                CurrentState = State.Idle;
                RefreshGui();
            }
        }

        private enum State
        {
            Idle = 0,
            Ready = 1
        }

        #region GUI shit

        public static readonly DependencyProperty TargetPathProperty = DependencyProperty.Register(
            "TargetPath", typeof(string), typeof(MainWindow), new PropertyMetadata(default(string)));

        public static readonly DependencyProperty ActualCodePageProperty = DependencyProperty.Register(
            "ActualCodePage", typeof(string), typeof(MainWindow), new PropertyMetadata(default(string)));

        public static readonly DependencyProperty ReadAsCodePageProperty = DependencyProperty.Register(
            "ReadAsCodePage", typeof(string), typeof(MainWindow), new PropertyMetadata(default(string)));

        public string StatusBarMessage
        {
            get
            {
                switch (CurrentState)
                {
                    case State.Idle:
                        return "Click \"Load file list\" to start.";
                    case State.Ready:
                        return "Ready.";
                    default:
                        return "Something weird happened :P";
                }
            }
        }

        public bool IsIdle => CurrentState == State.Idle;
        public bool IsReady => CurrentState == State.Ready;

        private const string InvalidCodePage = "???";

        public string ActualCodePageName
        {
            get
            {
                try
                {
                    return Encoding.GetEncoding(int.Parse(ActualCodePage)).EncodingName;
                }
                catch
                {
                    return InvalidCodePage;
                }
            }
        }

        public string ReadAsCodePageName
        {
            get
            {
                try
                {
                    return Encoding.GetEncoding(int.Parse(ReadAsCodePage)).EncodingName;
                }
                catch
                {
                    return InvalidCodePage;
                }
            }
        }

        public string Preview { get; private set; } = "[Output will appear here]";

        private void RefreshGui()
        {
            NotifyPropertyChanged(nameof(StatusBarMessage));
            NotifyPropertyChanged(nameof(ActualCodePageName));
            NotifyPropertyChanged(nameof(ReadAsCodePageName));
            NotifyPropertyChanged(nameof(IsIdle));
            NotifyPropertyChanged(nameof(IsReady));
            NotifyPropertyChanged(nameof(Preview));
        }

        private void TargetPathTextBox_OnPreviewDrag(object sender, DragEventArgs e)
        {
            if (!e.Data.GetFormats().Contains(DataFormats.FileDrop)) return;
            e.Effects = DragDropEffects.Link;
            e.Handled = true;
        }

        private void TargetPathTextBox_OnPreviewDrop(object sender, DragEventArgs e)
        {
            TargetPath = (e.Data.GetData(DataFormats.FileDrop) as string[])?.FirstOrDefault();
            RefreshGui();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void NotifyPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}