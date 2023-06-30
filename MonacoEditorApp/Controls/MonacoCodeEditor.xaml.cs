#nullable enable

using System;
using System.Diagnostics;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;

using Windows.UI.Xaml.Data;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Clipboard = Windows.ApplicationModel.DataTransfer.Clipboard;
using Monaco;
using Monaco.Editor;
using MonacoEditorApp;

namespace DevToys.UI.Controls
{
    public sealed partial class MonacoCodeEditor : UserControl, IDisposable
    {
        private readonly object _lockObject = new();
        private int _MonacoCodeEditorCodeReloadTentative;
        private CodeEditor _MonacoCodeEditorCore;


        public static readonly DependencyProperty HeaderProperty
            = DependencyProperty.Register(
                nameof(Header),
                typeof(object),
                typeof(MonacoCodeEditor),
                new PropertyMetadata(null, (d, e) => { ((MonacoCodeEditor)d).UpdateUI(); }));

        public string? Header
        {
            get => (string?)GetValue(HeaderProperty);
            set => SetValue(HeaderProperty, value);
        }

        public static readonly DependencyProperty ErrorMessageProperty
            = DependencyProperty.Register(
                nameof(ErrorMessage),
                typeof(string),
                typeof(MonacoCodeEditor),
                new PropertyMetadata(string.Empty));

        public string ErrorMessage
        {
            get => (string)GetValue(ErrorMessageProperty);
            set => SetValue(ErrorMessageProperty, value);
        }

        public static readonly DependencyProperty IsReadOnlyProperty
            = DependencyProperty.Register(
                nameof(IsReadOnly),
                typeof(bool),
                typeof(MonacoCodeEditor),
                new PropertyMetadata(false, OnIsReadOnlyPropertyChangedCalled));

        public bool IsReadOnly
        {
            get => (bool)GetValue(IsReadOnlyProperty);
            set => SetValue(IsReadOnlyProperty, value);
        }

        public static readonly DependencyProperty CanCopyWhenNotReadOnlyProperty
            = DependencyProperty.Register(
                nameof(CanCopyWhenNotReadOnly),
                typeof(bool),
                typeof(MonacoCodeEditor),
                new PropertyMetadata(false, (d, e) => { ((MonacoCodeEditor)d).UpdateUI(); }));

        public bool CanCopyWhenNotReadOnly
        {
            get => (bool)GetValue(CanCopyWhenNotReadOnlyProperty);
            set => SetValue(CanCopyWhenNotReadOnlyProperty, value);
        }

        public static DependencyProperty CodeLanguageProperty { get; }
            = DependencyProperty.Register(
                nameof(CodeLanguage),
                typeof(string),
                typeof(MonacoCodeEditor),
                new PropertyMetadata(string.Empty));

        public string? CodeLanguage
        {
            get => (string?)GetValue(CodeLanguageProperty);
            set => SetValue(CodeLanguageProperty, value);
        }

        public static readonly DependencyProperty TextProperty
            = DependencyProperty.Register(
                nameof(Text),
                typeof(string),
                typeof(MonacoCodeEditor),
                new PropertyMetadata(string.Empty));

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public static readonly DependencyProperty IsDiffViewModeProperty
            = DependencyProperty.Register(
                nameof(IsDiffViewMode),
                typeof(bool),
                typeof(MonacoCodeEditor),
                new PropertyMetadata(false));

        public bool IsDiffViewMode
        {
            get => (bool)GetValue(IsDiffViewModeProperty);
            set => SetValue(IsDiffViewModeProperty, value);
        }

        public static readonly DependencyProperty DiffLeftTextProperty
            = DependencyProperty.Register(
                nameof(DiffLeftText),
                typeof(string),
                typeof(MonacoCodeEditor),
                new PropertyMetadata(string.Empty));

        public string DiffLeftText
        {
            get => (string)GetValue(DiffLeftTextProperty);
            set => SetValue(DiffLeftTextProperty, value);
        }

        public static readonly DependencyProperty DiffRightTextProperty
            = DependencyProperty.Register(
                nameof(DiffRightText),
                typeof(string),
                typeof(MonacoCodeEditor),
                new PropertyMetadata(string.Empty));

        public string DiffRightText
        {
            get => (string)GetValue(DiffRightTextProperty);
            set => SetValue(DiffRightTextProperty, value);
        }

        public static readonly DependencyProperty InlineDiffViewModeProperty
            = DependencyProperty.Register(
                nameof(InlineDiffViewMode),
                typeof(bool),
                typeof(MonacoCodeEditor),
                new PropertyMetadata(
                    false,
                    (d, e) =>
                    {
                        lock (((MonacoCodeEditor)d)._lockObject)
                        {
                            ((MonacoCodeEditor)d)._MonacoCodeEditorCore.DiffOptions.RenderSideBySide = !(bool)e.NewValue;
                        }
                    }));

        public bool InlineDiffViewMode
        {
            get => (bool)GetValue(InlineDiffViewModeProperty);
            set => SetValue(InlineDiffViewModeProperty, value);
        }

        public static readonly DependencyProperty AllowExpandProperty
            = DependencyProperty.Register(
                nameof(AllowExpand),
                typeof(bool),
                typeof(MonacoCodeEditor),
                new PropertyMetadata(false));

        public bool AllowExpand
        {
            get => (bool)GetValue(AllowExpandProperty);
            set => SetValue(AllowExpandProperty, value);
        }

        public bool IsExpanded { get; private set; }

        public event EventHandler? ExpandedChanged;

        public MonacoCodeEditor()
        {
            InitializeComponent();

            _MonacoCodeEditorCore = ReloadMonacoCodeEditorCore();

            UpdateUI();
        }

        public void Dispose()
        {
            lock (_lockObject)
            {
                _MonacoCodeEditorCore.Dispose();
            }
        }



        private Button GetExpandButton()
        {
            return (Button)(ExpandButton ?? FindName(nameof(ExpandButton)));
        }

        private FontIcon GetExpandButtonIcon()
        {
            return (FontIcon)(ExpandButtonIcon ?? FindName(nameof(ExpandButtonIcon)));
        }

        private Button GetCopyButton()
        {
            return (Button)(CopyButton ?? FindName(nameof(CopyButton)));
        }

        private Button GetPasteButton()
        {
            return (Button)(PasteButton ?? FindName(nameof(PasteButton)));
        }

        private Button GetOpenFileButton()
        {
            return (Button)(OpenFileButton ?? FindName(nameof(OpenFileButton)));
        }

        private Button GetClearButton()
        {
            return (Button)(ClearButton ?? FindName(nameof(ClearButton)));
        }

        private TextBlock GetHeaderTextBlock()
        {
            return (TextBlock)(HeaderTextBlock ?? FindName(nameof(HeaderTextBlock)));
        }

        private CodeEditor ReloadMonacoCodeEditorCore()
        {
            lock (_lockObject)
            {
                if (_MonacoCodeEditorCore is not null)
                {
                    _MonacoCodeEditorCore.Loading -= MonacoCodeEditorCore_Loading;
                    _MonacoCodeEditorCore.InternalException -= MonacoCodeEditorCore_InternalException;
                    _MonacoCodeEditorCore.SetBinding(MonacoCodeEditor.CodeLanguageProperty, new Binding());
                    _MonacoCodeEditorCore.SetBinding(MonacoCodeEditor.TextProperty, new Binding());
                    _MonacoCodeEditorCore.SetBinding(MonacoCodeEditor.IsDiffViewModeProperty, new Binding());
                    _MonacoCodeEditorCore.SetBinding(MonacoCodeEditor.DiffLeftTextProperty, new Binding());
                    _MonacoCodeEditorCore.SetBinding(MonacoCodeEditor.DiffRightTextProperty, new Binding());
                    _MonacoCodeEditorCore.SetBinding(AutomationProperties.LabeledByProperty, new Binding());
                    CodeEditorContainer.Children.Clear();
                    _MonacoCodeEditorCore.Dispose();
                }

                _MonacoCodeEditorCore = new CodeEditor();
                _MonacoCodeEditorCore.Loading += MonacoCodeEditorCore_Loading;
                _MonacoCodeEditorCore.InternalException += MonacoCodeEditorCore_InternalException;
                _MonacoCodeEditorCore.TabIndex = 0;

                _MonacoCodeEditorCore.SetBinding(
                    MonacoCodeEditor.CodeLanguageProperty,
                    new Binding()
                    {
                        Path = new PropertyPath(nameof(CodeLanguage)),
                        Source = this,
                        Mode = BindingMode.OneWay
                    });

                _MonacoCodeEditorCore.SetBinding(
                    MonacoCodeEditor.TextProperty,
                    new Binding()
                    {
                        Path = new PropertyPath(nameof(Text)),
                        Source = this,
                        Mode = BindingMode.TwoWay,
                        UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                    });

                _MonacoCodeEditorCore.SetBinding(
                    MonacoCodeEditor.IsDiffViewModeProperty,
                    new Binding()
                    {
                        Path = new PropertyPath(nameof(IsDiffViewMode)),
                        Source = this,
                        Mode = BindingMode.OneWay
                    });

                _MonacoCodeEditorCore.SetBinding(
                    MonacoCodeEditor.DiffLeftTextProperty,
                    new Binding()
                    {
                        Path = new PropertyPath(nameof(DiffLeftText)),
                        Source = this,
                        Mode = BindingMode.TwoWay,
                        UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                    });

                _MonacoCodeEditorCore.SetBinding(
                    MonacoCodeEditor.DiffRightTextProperty,
                    new Binding()
                    {
                        Path = new PropertyPath(nameof(DiffRightText)),
                        Source = this,
                        Mode = BindingMode.TwoWay,
                        UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                    });

                _MonacoCodeEditorCore.SetBinding(
                    AutomationProperties.LabeledByProperty,
                    new Binding()
                    {
                        ElementName = nameof(HeaderTextBlock),
                        Source = this,
                        Mode = BindingMode.OneTime
                    });

                CodeEditorContainer.Children.Add(_MonacoCodeEditorCore);
                return _MonacoCodeEditorCore;
            }
        }

        private void MonacoCodeEditorCore_InternalException(CodeEditor sender, Exception args)
        {
            if (_MonacoCodeEditorCodeReloadTentative >= 5)
            {
                ErrorMessage = $"{args.Message}\r\n{args.InnerException?.Message}";
                Debug.WriteLine(ErrorMessage);
            }
            else
            {
                try
                {
                    ReloadMonacoCodeEditorCore();
                }
                catch(Exception ex)
                {
                }
            }

            _MonacoCodeEditorCodeReloadTentative++;
        }

        private void MonacoCodeEditorCore_Loading(object sender, RoutedEventArgs e)
        {
            lock (_lockObject)
            {
                _MonacoCodeEditorCore.Loading -= MonacoCodeEditorCore_Loading;

                //_MonacoCodeEditorCore.HasGlyphMargin = false;
                //_MonacoCodeEditorCore.Options.GlyphMargin = false;
                //_MonacoCodeEditorCore.Options.MouseWheelZoom = false;
                //_MonacoCodeEditorCore.Options.OverviewRulerBorder = false;
                //_MonacoCodeEditorCore.Options.ScrollBeyondLastLine = false;
                //_MonacoCodeEditorCore.Options.FontLigatures = true;
                //_MonacoCodeEditorCore.Options.SnippetSuggestions = SnippetSuggestions.None;
                //_MonacoCodeEditorCore.Options.CodeLens = false;
                //_MonacoCodeEditorCore.Options.QuickSuggestions = false;
                //_MonacoCodeEditorCore.Options.WordBasedSuggestions = false;
                //_MonacoCodeEditorCore.Options.Minimap = new EditorMinimapOptions()
                //{
                //    Enabled = false
                //};
                //_MonacoCodeEditorCore.Options.Hover = new EditorHoverOptions()
                //{
                //    Enabled = false
                //};

                //_MonacoCodeEditorCore.DiffOptions.GlyphMargin = false;
                //_MonacoCodeEditorCore.DiffOptions.MouseWheelZoom = false;
                //_MonacoCodeEditorCore.DiffOptions.OverviewRulerBorder = false;
                //_MonacoCodeEditorCore.DiffOptions.ScrollBeyondLastLine = false;
                //_MonacoCodeEditorCore.DiffOptions.FontLigatures = true;
                //_MonacoCodeEditorCore.DiffOptions.SnippetSuggestions = SnippetSuggestions.None;
                //_MonacoCodeEditorCore.DiffOptions.CodeLens = false;
                //_MonacoCodeEditorCore.DiffOptions.QuickSuggestions = false;
                //_MonacoCodeEditorCore.DiffOptions.Minimap = new EditorMinimapOptions()
                //{
                //    Enabled = false
                //};
                //_MonacoCodeEditorCore.DiffOptions.Hover = new EditorHoverOptions()
                //{
                //    Enabled = false
                //};

                //ApplySettings();
            }
        }

        private void ApplySettings()
        {
            lock (_lockObject)
            {
                _MonacoCodeEditorCore.Options.WordWrapMinified = true;
                _MonacoCodeEditorCore.Options.WordWrap = WordWrap.On;
                _MonacoCodeEditorCore.Options.LineNumbers = LineNumbersType.On ;
                _MonacoCodeEditorCore.Options.RenderLineHighlight = RenderLineHighlight.All ;
                _MonacoCodeEditorCore.Options.RenderWhitespace = RenderWhitespace.All;
                _MonacoCodeEditorCore.Options.FontFamily = "Cascadia Mono";
                _MonacoCodeEditorCore.DiffOptions.WordWrapMinified = true;
                _MonacoCodeEditorCore.DiffOptions.WordWrap = WordWrap.On;
                _MonacoCodeEditorCore.DiffOptions.LineNumbers =  LineNumbersType.On;
                _MonacoCodeEditorCore.DiffOptions.RenderLineHighlight = RenderLineHighlight.All;
                _MonacoCodeEditorCore.DiffOptions.RenderWhitespace =  RenderWhitespace.All ;
                _MonacoCodeEditorCore.DiffOptions.FontFamily = "Cascadia Mono";
            }
        }

        private void UpdateUI()
        {
            if (Header is not null)
            {
                GetHeaderTextBlock().Visibility = Visibility.Visible;
            }

            if (IsReadOnly)
            {
                GetCopyButton().Visibility = Visibility.Visible;
                if (PasteButton is not null)
                {
                    PasteButton.Visibility = Visibility.Collapsed;
                    OpenFileButton.Visibility = Visibility.Collapsed;
                    ClearButton.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                if (CanCopyWhenNotReadOnly)
                {
                    GetCopyButton().Visibility = Visibility.Visible;
                }
                else if (CopyButton is not null)
                {
                    CopyButton.Visibility = Visibility.Collapsed;
                }

                GetPasteButton().Visibility = Visibility.Visible;
                GetOpenFileButton().Visibility = Visibility.Visible;
                GetClearButton().Visibility = Visibility.Visible;
                GetPasteButton().Visibility = Visibility.Visible;
            }

            if (IsDiffViewMode)
            {
                if (CopyButton is not null)
                {
                    CopyButton.Visibility = Visibility.Collapsed;
                }

                if (PasteButton is not null)
                {
                    PasteButton.Visibility = Visibility.Collapsed;
                    OpenFileButton.Visibility = Visibility.Collapsed;
                    ClearButton.Visibility = Visibility.Collapsed;
                }
            }

            if (AllowExpand)
            {
                GetExpandButton().Visibility = Visibility.Visible;
            }
        }

        private void ExpandButton_Click(object _, RoutedEventArgs e)
        {
            IsExpanded = !IsExpanded;
            ExpandedChanged?.Invoke(this, EventArgs.Empty);

            if (IsExpanded)
            {
                GetExpandButtonIcon().Glyph = "\uF165";
                ToolTipService.SetToolTip(GetExpandButton(), "IsExpanded");
            }
            else
            {
                GetExpandButtonIcon().Glyph = "\uF15F";
                ToolTipService.SetToolTip(GetExpandButton(), "IsExpanded");
            }
        }

        private async void PasteButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                DataPackageView? dataPackageView = Clipboard.GetContent();
                if (!dataPackageView.Contains(StandardDataFormats.Text))
                {
                    return;
                }

                string? text = await dataPackageView.GetTextAsync();

                lock (_lockObject)
                {
                    //_MonacoCodeEditorCore.Text = string.Empty;

                    _MonacoCodeEditorCore.SelectedText = text;
                    _MonacoCodeEditorCore.Focus(FocusState.Programmatic);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Failed to paste in code editor", ex);
            }
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var data = new DataPackage
                {
                    RequestedOperation = DataPackageOperation.Copy
                };
                data.SetText(Text ?? string.Empty);

                Clipboard.SetContentWithOptions(data, new ClipboardContentOptions() { IsAllowedInHistory = true, IsRoamable = true });
                Clipboard.Flush();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Failed to copy from code editor", ex);
            }
        }

        private async void OpenFileButton_Click(object sender, RoutedEventArgs e)
        {
            var filePicker = new FileOpenPicker
            {
                ViewMode = PickerViewMode.List,
                SuggestedStartLocation = PickerLocationId.ComputerFolder
            };

            filePicker.FileTypeFilter.Add("*");
            filePicker.FileTypeFilter.Add(".txt");
            filePicker.FileTypeFilter.Add(".js");
            filePicker.FileTypeFilter.Add(".ts");
            filePicker.FileTypeFilter.Add(".cs");
            filePicker.FileTypeFilter.Add(".java");
            filePicker.FileTypeFilter.Add(".xml");
            filePicker.FileTypeFilter.Add(".json");
            filePicker.FileTypeFilter.Add(".md");
            filePicker.FileTypeFilter.Add(".sql");

            StorageFile file = await filePicker.PickSingleFileAsync();
            if (file is not null)
            {
                try
                {
                    string? text = await FileIO.ReadTextAsync(file);
                    await Dispatcher.RunAsync(
                        Windows.UI.Core.CoreDispatcherPriority.Normal,
                        () =>
                        {
                            Text = text;
                        });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Failed to load a file into a code editor", ex);

                    this.DispatcherQueue.TryEnqueue(async () =>
                    {
                        var confirmationDialog = new ContentDialog
                        {
                            Title = "test",
                            Content = $"{file.Name}",
                            CloseButtonText ="Ok",
                            PrimaryButtonText = "cancel",
                            DefaultButton = ContentDialogButton.Close
                        };

                        if (await confirmationDialog.ShowAsync() == ContentDialogResult.Primary)
                        {
                           
                        }
                    });
                }
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            Text = string.Empty;
        }

        private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Width < CommandsToolBar.ActualWidth + 100)
            {
                CommandsToolBar.Visibility = Visibility.Collapsed;
            }
            else
            {
                CommandsToolBar.Visibility = Visibility.Visible;
            }
        }

        private static void OnIsReadOnlyPropertyChangedCalled(DependencyObject sender, DependencyPropertyChangedEventArgs eventArgs)
        {
            var MonacoCodeEditor = (MonacoCodeEditor)sender;
            lock (MonacoCodeEditor._lockObject)
            {
                MonacoCodeEditor._MonacoCodeEditorCore.ReadOnly = (bool)eventArgs.NewValue;
            }
            MonacoCodeEditor.UpdateUI();
        }
    }
}
